using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NightFlux
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                var configuration = LoadConfiguration(args);
                using var nsql = await NightSql.GetInstance(configuration);
                await nsql.StartBatchImport();
                using(var sync = new NightSync(configuration))
                {
                    await Task.WhenAll(
                            sync.ImportBg(nsql),
                            sync.ImportBasalProfiles(nsql),
                            sync.ImportTempBasals(nsql),
                            sync.ImportBoluses(nsql),
                            sync.ImportCarbs(nsql)
                        );
                }
                await nsql.FinalizeBatchImport();
                configuration.Save();
            }
            catch(Exception e)
            {
                Console.WriteLine($"Exiting due to error:\n{e}");
            }
            Console.WriteLine($"Finished");
        }

        private static Configuration LoadConfiguration(string[] args)
        {
            try
            {
                if (args.Length > 0)
                    return Configuration.Load(args[0]);

                return Configuration.Load();
            }
            catch(Exception e)
            {
                Console.WriteLine($"Error reading configuration:\n{e}");
                throw;
            }
        }

        //private static async Task AnalyticsTest(CsvReader reader)
        //{
        //    var now = DateTimeOffset.UtcNow;
        //    var allBgs = await reader.GetBgValues(now.AddDays(-3));
        //    Transforms.Compose(allBgs);
        //}
    }
}
