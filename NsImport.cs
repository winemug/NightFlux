﻿using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NightFlux
{
    public class NsImport : IDisposable
    {

        private string MongoUrl;
        private string MongoDatabaseName;
        private ulong LastBgTimestamp;

        private IConfigurationSection ConfigurationSection;

        public NsImport(IConfigurationSection cs)
        {
            ConfigurationSection = cs;
            MongoUrl = cs["mongo_url"];
            MongoDatabaseName = cs["mongo_database_name"];
            LastBgTimestamp = ulong.Parse(cs["last_bg_timestamp"] ?? "0" );
        }

        public void Dispose()
        {
        }

        public async IAsyncEnumerable<BgValue> BgEntries()
        {
            var mc = new MongoClient(MongoUrl);
            var mdb = mc.GetDatabase(MongoDatabaseName);

            var entries = mdb.GetCollection<BsonDocument>("entries");
            var filter = new FilterDefinitionBuilder<BsonDocument>()
                .And(
                    new FilterDefinitionBuilder<BsonDocument>().Gt<ulong>("date", LastBgTimestamp),
                    new FilterDefinitionBuilder<BsonDocument>().Eq<string>("type", "sgv")
                );

            using var cursor = await entries.Find(filter).ToCursorAsync();
            while (await cursor.MoveNextAsync())
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    DateTimeOffset? dt = null;
                    decimal? gv = null;
                    try
                    {
                        dt = DateTimeOffset.FromUnixTimeMilliseconds(document["date"].ToInt64()).UtcDateTime;
                        gv = document["sgv"].ToDecimal();
                    }
                    catch
                    {
                        dt = null;
                        gv = null;
                    }

                    if (dt.HasValue && gv.HasValue)
                    {
                        yield return new BgValue
                        {
                            Value = gv.Value,
                            Time =  dt.Value
                        };
                    }
                }
            }

            ConfigurationSection["LastBgTimestamp"] = LastBgTimestamp.ToString();
        }
    }
}
