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
                using(var sync = new NightSync(configuration))
                {
                    await Task.WhenAll(
                            sync.ImportBg(),
                            sync.ImportBasals(),
                            sync.ImportBoluses(),
                            sync.ImportCarbs()
                        );
                }
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

        //private static async Task ImportBgv(NsExport exporter, CsvImport importer, long timestamp)
        //{
        //    try
        //    {
        //        await foreach(var bgv in exporter.BgEntries(timestamp))
        //        {
        //            await importer.ImportBg(bgv);
        //        }
        //    }
        //    catch(Exception e)
        //    {
        //        Console.WriteLine($"Error importing bg values:\n{e}");
        //        throw;
        //    }
        //}

        //private static async Task ImportProfilesAndBasals(NsExport exporter, CsvImport importer)
        //{
        //    try
        //    {
        //        var basalProfiles = new List<BasalProfile>();
        //        await foreach(var profile in exporter.ProfileEntries())
        //        {
        //            await importer.ImportProfile(profile);
        //            basalProfiles.Add(profile);
        //        }

        //        await foreach(var basalRate in exporter.BasalRates(basalProfiles))
        //        {
        //            await importer.ImportBasalRate(basalRate);
        //        }

        //    }
        //    catch(Exception e)
        //    {
        //        Console.WriteLine($"Error importing basal profiles:\n{e}");
        //        throw;
        //    }
        //}

        //private static async Task AnalyticsTest(CsvReader reader)
        //{
        //    var now = DateTimeOffset.UtcNow;
        //    var allBgs = await reader.GetBgValues(now.AddDays(-3));
        //    Transforms.Compose(allBgs);
        //}
    }
}
