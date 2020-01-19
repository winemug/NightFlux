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
        private string ConfigurationPath { get; set;}
        public long LastSync { get; set; }

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
                    NsMongoDbUrl = "mongodb://user:pass@server:port/collection?ssl=false",
                    SqlitePath = "nightflux.sqlite",
                    NsDbName = "nightscout",
                    LastSync = 0
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
