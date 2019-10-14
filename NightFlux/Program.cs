using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace NightFlux
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                var configuration = GetConfiguration(args);
                Console.WriteLine("Starting import from nightscout");

                using var exporter = new NsExport(configuration.GetSection("nightscout"));
                using var importer = new FluxImport(configuration.GetSection("influxdb"));
                await Task.WhenAll(
                        ImportBgv(exporter, importer)
                    );
            }
            catch(Exception e)
            {
                Console.WriteLine($"Exiting due to error:\n{e}");
            }
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

        private static async Task ImportBgv(NsExport exporter, FluxImport importer)
        {
            try
            {
                await foreach(var bgv in exporter.BgEntries())
                {
                    importer.QueueImport(bgv);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine($"Error importing bg values:\n{e}");
                throw;
            }
        }
    }
}
