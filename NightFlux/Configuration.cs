using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NightFlux
{
    public class Configuration
    {
        public string SqlitePath {get; set;}
        public string NsMongoDbUrl {get; set;}
        public string NsDbName {get; set;}
        public int NsImportLastBgTimestamp {get; set;}
        private string ConfigurationPath { get; set;}

        public static Configuration Load(string path = "./NightFlux.json")
        {
            Configuration result;
            if (File.Exists(path))
            {
                var jsonText = File.ReadAllText(path);
                result = JsonConvert.DeserializeObject<Configuration>(jsonText);
            }
            else
            {
                result = new Configuration()
                {
                    SqlitePath = "nightflux.sqlite",
                    NsDbName = "nightscout"
                };
            }
            result.ConfigurationPath = path;
            return result;
        }

        public void Save()
        {
            var jsonText = JsonConvert.SerializeObject(this);
            File.WriteAllText(ConfigurationPath, jsonText);
        }
    }
}
