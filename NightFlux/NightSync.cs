using MongoDB.Bson;
using MongoDB.Driver;
using System.Data.SQLite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NightFlux
{
    public class NightSync : IDisposable
    {
        private MongoClient MongoClient;
        private IMongoDatabase MongoDatabase;
        private Configuration Configuration;
        private NightSql NightSql;

        public NightSync(Configuration configuration)
        {
            MongoClient = new MongoClient(configuration.NsMongoDbUrl);
            MongoDatabase = MongoClient.GetDatabase(configuration.NsDbName);
            Configuration = configuration;
        }

        public void Dispose()
        {
            NightSql?.Dispose();
        }

        public async Task ImportBg()
        {
            var nsql = await NightSql.GetInstance(Configuration);
            var lastTimestamp = await nsql.GetLastBgDate();
            var entries = MongoDatabase.GetCollection<BsonDocument>("entries");
            var filter = new FilterDefinitionBuilder<BsonDocument>()
                .And(
                    new FilterDefinitionBuilder<BsonDocument>().Gt<long>("date", lastTimestamp),
                    new FilterDefinitionBuilder<BsonDocument>().Eq<string>("type", "sgv")
                );

            using var cursor = await entries.Find(filter).ToCursorAsync();
            await nsql.StartBatchImportBg();
            while (await cursor.MoveNextAsync())
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    DateTimeOffset? dt = null;
                    decimal? gv = null;
                    try
                    {
                        dt = DateTimeOffset.FromUnixTimeMilliseconds(document["date"].ToInt64()).UtcDateTime;
                        gv = document["sgv"].ToDouble().ToPreciseDecimal(1);
                    }
                    catch
                    {
                        dt = null;
                        gv = null;
                    }

                    if (dt.HasValue && gv.HasValue)
                    {
                        await nsql.ImportBg(new BgValue
                        {
                            Value = gv.Value,
                            Time = dt.Value
                        });
                    }
                }
            }
            await nsql.FinalizeBatchImportBg();
        }

        public async Task ImportBasalProfiles()
        {
        }

        public async Task ImportBoluses()
        {
        }

        public async Task ImportCarbs()
        {
        }
    }
}
