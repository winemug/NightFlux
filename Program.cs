using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace NightFlux
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var configurationPath = @".\NightFlux.json";
            if (args.Length > 0)
            {
                configurationPath = args[0];
            }
            var configuration = new ConfigurationBuilder().AddJsonFile(configurationPath).Build();
            using var importer = new NsImport(configuration.GetSection("nightscout"));

            await foreach(var bgv in importer.BgEntries())
            {
                Console.WriteLine($"bgv: {bgv.Value}");
            }
        }
    }
}
