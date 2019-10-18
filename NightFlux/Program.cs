using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using NightLight;

namespace NightFlux
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                var configuration = GetConfiguration(args);

                long bgTimestamp = 0;
                using (var csvr = new CsvReader(configuration.GetSection("csv")))
                {
                    bgTimestamp = await csvr.GetLastBgTimestamp();
                }

                using var exporter = new NsExport(configuration.GetSection("nightscout"));
                //using var importer = new FluxImport(configuration.GetSection("influxdb"));
                using(var importer = new CsvImport(configuration.GetSection("csv")))
                {
                    await Task.WhenAll(
                            ImportBgv(exporter, importer, bgTimestamp),
                            ImportProfilesAndBasals(exporter, importer)
                        );
                }


                using (var csvr = new CsvReader(configuration.GetSection("csv")))
                {
                    await AnalyticsTest(csvr);
                }

            }
            catch(Exception e)
            {
                Console.WriteLine($"Exiting due to error:\n{e}");
            }
            Console.WriteLine($"Finished");
        }

        private static IConfiguration GetConfiguration(string[] args)
        {
            try
            {
                var configurationPath = @".\NightFlux.json";
                if (args.Length > 0)
                {
                    configurationPath = args[0];
                }
                return new ConfigurationBuilder().AddJsonFile(configurationPath).Build();
            }
            catch(Exception e)
            {
                Console.WriteLine($"Error reading configuration:\n{e}");
                throw;
            }
        }

        private static async Task ImportBgv(NsExport exporter, CsvImport importer, long timestamp)
        {
            try
            {
                await foreach(var bgv in exporter.BgEntries(timestamp))
                {
                    await importer.ImportBg(bgv);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine($"Error importing bg values:\n{e}");
                throw;
            }
        }

        private static async Task ImportProfilesAndBasals(NsExport exporter, CsvImport importer)
        {
            try
            {
                var basalProfiles = new List<BasalProfile>();
                await foreach(var profile in exporter.ProfileEntries())
                {
                    await importer.ImportProfile(profile);
                    basalProfiles.Add(profile);
                }

                await foreach(var basalRate in exporter.BasalRates(basalProfiles))
                {
                    await importer.ImportBasalRate(basalRate);
                }

            }
            catch(Exception e)
            {
                Console.WriteLine($"Error importing basal profiles:\n{e}");
                throw;
            }
        }

        private static async Task AnalyticsTest(CsvReader reader)
        {
            var now = DateTimeOffset.UtcNow;
            var allBgs = await reader.GetBgValues(now.AddDays(-3));
            Transforms.Compose(allBgs);
        }
    }
}
