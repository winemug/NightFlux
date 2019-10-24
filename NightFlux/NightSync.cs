using MongoDB.Bson;
using MongoDB.Driver;
using System.Data.SQLite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TimeZoneConverter;
using Newtonsoft.Json.Linq;
using NightFlux.Model;

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
            await nsql.StartBatchImport();
            var lastTimestamp = await nsql.GetLastBgDate();
            var entries = MongoDatabase.GetCollection<BsonDocument>("entries");
            var filter = new FilterDefinitionBuilder<BsonDocument>()
                .And(
                    new FilterDefinitionBuilder<BsonDocument>().Gt<long>("date", lastTimestamp),
                    new FilterDefinitionBuilder<BsonDocument>().Eq<string>("type", "sgv")
                );

            using var cursor = await entries.Find(filter).ToCursorAsync();
            while (await cursor.MoveNextAsync())
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    DateTimeOffset? dt = document.SafeDateTimeOffset("date");
                    decimal? gv = document.SafePreciseDecimal("sgv", 1);

                    if (dt.HasValue && gv.HasValue)
                    {
                        await nsql.Import(new BgValue
                        {
                            Value = gv.Value,
                            Time = dt.Value
                        });
                    }
                }
            }

            await nsql.FinalizeBatchImport();
        }

        public async Task ImportBasalProfiles()
        {
            var nsql = await NightSql.GetInstance(Configuration);
            await nsql.StartBatchImport();
            var lastTimestamp = await nsql.GetLastProfileChangeDate();
            var entries = MongoDatabase.GetCollection<BsonDocument>("treatments");
            var filter = new FilterDefinitionBuilder<BsonDocument>()
                    .And(
                        new FilterDefinitionBuilder<BsonDocument>().Eq<string>("eventType", "Profile Switch"),
                        new FilterDefinitionBuilder<BsonDocument>().Exists("profileJson"),
                        new FilterDefinitionBuilder<BsonDocument>()
                            .Or(
                                new FilterDefinitionBuilder<BsonDocument>()
                                    .And(
                                        new FilterDefinitionBuilder<BsonDocument>().Exists("NSCLIENT_ID"),
                                        new FilterDefinitionBuilder<BsonDocument>().Gt<double>("NSCLIENT_ID", lastTimestamp)),
                                new FilterDefinitionBuilder<BsonDocument>()
                                    .And(
                                        new FilterDefinitionBuilder<BsonDocument>().Exists("timestamp"),
                                        new FilterDefinitionBuilder<BsonDocument>().Gt<double>("timestamp", lastTimestamp))));

            using var cursor = await entries.Find(filter).ToCursorAsync();
            while (await cursor.MoveNextAsync())
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    var basalProfile = await ParseProfileSwitch(document);
                    if (basalProfile.HasValue)
                        await nsql.Import(basalProfile.Value);
                }
            }
            await nsql.FinalizeBatchImport();
        }

        public async Task ImportTempBasals()
        {
            var nsql = await NightSql.GetInstance(Configuration);
            await nsql.StartBatchImport();
            var lastTimestamp = await nsql.GetLastTempBasalDate();

            var entries = MongoDatabase.GetCollection<BsonDocument>("treatments");
            var filter = new FilterDefinitionBuilder<BsonDocument>()
                    .And(
                        new FilterDefinitionBuilder<BsonDocument>().Eq<string>("eventType", "Temp Basal"),
                        new FilterDefinitionBuilder<BsonDocument>()
                            .Or(
                                new FilterDefinitionBuilder<BsonDocument>()
                                    .And(
                                        new FilterDefinitionBuilder<BsonDocument>().Exists("NSCLIENT_ID"),
                                        new FilterDefinitionBuilder<BsonDocument>().Gt<double>("NSCLIENT_ID", lastTimestamp)),
                                new FilterDefinitionBuilder<BsonDocument>()
                                    .And(
                                        new FilterDefinitionBuilder<BsonDocument>().Exists("timestamp"),
                                        new FilterDefinitionBuilder<BsonDocument>().Gt<double>("timestamp", lastTimestamp))));

            using var cursor = await entries.Find(filter).ToCursorAsync();
            while (await cursor.MoveNextAsync())
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    var tempBasal = await ParseTempBasal(document);
                    if (tempBasal.HasValue)
                        await nsql.Import(tempBasal.Value);
                }
            }
            await nsql.FinalizeBatchImport();
        }

        public async Task ImportBoluses()
        {
            var nsql = await NightSql.GetInstance(Configuration);
            await nsql.StartBatchImport();
            var lastTimestamp = await nsql.GetLastBolusDate();

            var entries = MongoDatabase.GetCollection<BsonDocument>("treatments");
            var filter = new FilterDefinitionBuilder<BsonDocument>()
                    .And(
                        new FilterDefinitionBuilder<BsonDocument>().Gt<double>("insulin", 0),
                        new FilterDefinitionBuilder<BsonDocument>()
                            .Or(
                                new FilterDefinitionBuilder<BsonDocument>()
                                    .And(
                                        new FilterDefinitionBuilder<BsonDocument>().Exists("NSCLIENT_ID"),
                                        new FilterDefinitionBuilder<BsonDocument>().Gt<double>("NSCLIENT_ID", lastTimestamp)),
                                new FilterDefinitionBuilder<BsonDocument>()
                                    .And(
                                        new FilterDefinitionBuilder<BsonDocument>().Exists("timestamp"),
                                        new FilterDefinitionBuilder<BsonDocument>().Gt<double>("timestamp", lastTimestamp))));

            using var cursor = await entries.Find(filter).ToCursorAsync();
            while (await cursor.MoveNextAsync())
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    var bolus = await ParseBolus(document);
                    if (bolus.HasValue)
                        await nsql.Import(bolus.Value);
                }
            }
            await nsql.FinalizeBatchImport();
        }

        public async Task ImportCarbs()
        {
            var nsql = await NightSql.GetInstance(Configuration);
            var lastTimestamp = await nsql.GetLastCarbDate();

            var entries = MongoDatabase.GetCollection<BsonDocument>("treatments");
            var filter = new FilterDefinitionBuilder<BsonDocument>()
                    .And(
                        new FilterDefinitionBuilder<BsonDocument>().Gt<double>("carbs", 0),
                        new FilterDefinitionBuilder<BsonDocument>()
                            .Or(
                                new FilterDefinitionBuilder<BsonDocument>()
                                    .And(
                                        new FilterDefinitionBuilder<BsonDocument>().Exists("NSCLIENT_ID"),
                                        new FilterDefinitionBuilder<BsonDocument>().Gt<double>("NSCLIENT_ID", lastTimestamp)),
                                new FilterDefinitionBuilder<BsonDocument>()
                                    .And(
                                        new FilterDefinitionBuilder<BsonDocument>().Exists("timestamp"),
                                        new FilterDefinitionBuilder<BsonDocument>().Gt<double>("timestamp", lastTimestamp))));

            using var cursor = await entries.Find(filter).ToCursorAsync();
            while (await cursor.MoveNextAsync())
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    var carb = await ParseCarbs(document);
                    if (carb.HasValue)
                        await nsql.Import(carb.Value);
                }
            }
        }

        public async Task ImportExtendedBoluses()
        {
            var nsql = await NightSql.GetInstance(Configuration);
            await nsql.StartBatchImport();
            var lastTimestamp = await nsql.GetLastExtendedBolusDate();

            var entries = MongoDatabase.GetCollection<BsonDocument>("treatments");
            var filter = new FilterDefinitionBuilder<BsonDocument>()
                    .And(
                        new FilterDefinitionBuilder<BsonDocument>().Eq<string>("eventType", "Combo Bolus"),
                        new FilterDefinitionBuilder<BsonDocument>()
                            .Or(
                                new FilterDefinitionBuilder<BsonDocument>()
                                    .And(
                                        new FilterDefinitionBuilder<BsonDocument>().Exists("NSCLIENT_ID"),
                                        new FilterDefinitionBuilder<BsonDocument>().Gt<double>("NSCLIENT_ID", lastTimestamp)),
                                new FilterDefinitionBuilder<BsonDocument>()
                                    .And(
                                        new FilterDefinitionBuilder<BsonDocument>().Exists("timestamp"),
                                        new FilterDefinitionBuilder<BsonDocument>().Gt<double>("timestamp", lastTimestamp))));

            using var cursor = await entries.Find(filter).ToCursorAsync();
            while (await cursor.MoveNextAsync())
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    var extendedBolus = await ParseExtendedBolus(document);
                    if (extendedBolus.HasValue)
                        await nsql.Import(extendedBolus.Value);
                }
            }
            await nsql.FinalizeBatchImport();
        }

        private async Task<Carb?> ParseCarbs(BsonDocument document)
        {
            DateTimeOffset? eventTime = document.SafeDateTimeOffset("NSCLIENT_ID");
            if (!eventTime!.HasValue)
                eventTime = document.SafeDateTimeOffset("timestamp");

            if (!eventTime.HasValue)
                return null;

            var amount = document.SafePreciseDecimal("carbs", 0.1m);

            if (amount <= 0)
                return null;

            return new Carb {
                Time = eventTime.Value,
                Amount = amount.Value,
                ImportId = document.SafeString("created_at")
            };
        }

        private async Task<Bolus?> ParseBolus(BsonDocument document)
        {
            DateTimeOffset? eventTime = document.SafeDateTimeOffset("NSCLIENT_ID");
            if (!eventTime!.HasValue)
                eventTime = document.SafeDateTimeOffset("timestamp");

            if (!eventTime.HasValue)
                return null;

            var amount = document.SafePreciseDecimal("insulin", 0.05m);

            if (amount <= 0)
                return null;

            return new Bolus {
                Time = eventTime.Value,
                Amount = amount.Value
            };
        }

        private async Task<ExtendedBolus?> ParseExtendedBolus(BsonDocument document)
        {
            DateTimeOffset? eventTime = document.SafeDateTimeOffset("NSCLIENT_ID");
            if (!eventTime!.HasValue)
                eventTime = document.SafeDateTimeOffset("timestamp");

            if (!eventTime.HasValue)
                return null;

            var duration = document.SafeInt("duration") ?? 0;
            var amount = document.SafePreciseDecimal("enteredinsulin", 0.05m) ?? 0;
            var split = document.SafeInt("splitExt") ?? 0;
            amount = (amount * split / 100m).ToPreciseDecimal(0.05m);

            return new ExtendedBolus {
                Time = eventTime.Value,
                Duration = duration,
                Amount = amount
            };
        }

        private async Task<TempBasal?> ParseTempBasal(BsonDocument document)
        {
            DateTimeOffset? eventTime = document.SafeDateTimeOffset("NSCLIENT_ID");
            if (!eventTime!.HasValue)
                eventTime = document.SafeDateTimeOffset("timestamp");

            if (!eventTime.HasValue)
                return null;

            var duration = document.SafeInt("duration") ?? 0;
            var absoluteRate = document.SafePreciseDecimal("absolute", 0.05m);
            var percentage = document.SafeInt("percent");

            return new TempBasal {
                Time = eventTime.Value,
                Duration = duration,
                AbsoluteRate = absoluteRate,
                Percentage = percentage
            };
        }

        private async Task<BasalProfile?> ParseProfileSwitch(BsonDocument document)
        {
            BasalProfile? ret = null;

            DateTimeOffset? profileSwitchTime = document.SafeDateTimeOffset("NSCLIENT_ID");
            if (!profileSwitchTime.HasValue)
                profileSwitchTime = document.SafeDateTimeOffset("timestamp");

            if (!profileSwitchTime.HasValue)
                return null;

            var joProfile = document.SafeJsonObject("profileJson");
            if (joProfile == null)
                return null;

            try
            {
                int percentage = document.SafeInt("percentage") ?? 100;
                int timeShift = document.SafeInt("timeshift") ?? 0;
                int duration = document.SafeInt("duration") ?? 0;

                int utcOffset;
                if (joProfile.ContainsKey("timezone"))
                {
                    var tzString = joProfile["timezone"].ToString();
                    var tzi = TZConvert.GetTimeZoneInfo(tzString);
                    // including dst at the time of profile switch
                    // as the pod does not switch timezones or dst,
                    // there has to be an explicit profile switch entry for each change in local time
                    utcOffset = (int) tzi.GetUtcOffset(profileSwitchTime.Value).TotalMinutes;
                }
                else
                    utcOffset = document.SafeInt("utcOffset") ?? 0;

                var rates = await GetBasalRates(joProfile, percentage);
                if (rates != null)
                {
                    ret = new BasalProfile()
                    {
                        Time = profileSwitchTime.Value,
                        BasalRates = rates,
                        UtcOffsetInMinutes = utcOffset + timeShift,
                        Duration = duration
                    };
                }
            }
            catch { }
            return ret;
        }

        private async Task<decimal[]> GetBasalRates(JObject joProfile, int percentage)
        {
            var rates = new decimal[48];
            var nsRates = await GetSingleRates(joProfile, percentage);

            if (!nsRates[0].HasValue)
                return null;

            var lastRate = 0m;
            for(int i=0; i<48; i++)
            {
                if (nsRates[i].HasValue)
                {
                    lastRate = nsRates[i].Value;
                }
                rates[i] = lastRate;
            }
            return rates;
        }

        private async Task<decimal?[]> GetSingleRates(JObject joProfile, int percentage)
        {
            var nsRates = new decimal?[48];

            if (!joProfile.ContainsKey("basal"))
                return nsRates;

            foreach(JObject joRate in joProfile["basal"])
            {
                int? basalIndex = null;

                var nsRate = joRate.SafeDouble("value");
                if (!nsRate.HasValue)
                    continue;

                var rate = (nsRate.Value * percentage / 100).ToPreciseDecimal(0.05m);

                var tas = joRate.SafeInt("timeAsSeconds");
                if (tas.HasValue)
                {
                    basalIndex = (int) tas.Value / 1800;
                }
                
                var nsTimeString = joRate.SafeString("time");
                if (!string.IsNullOrEmpty(nsTimeString))
                {
                    var nsTimeComponents = nsTimeString.Split(':');
                    if (nsTimeComponents.Length == 2)
                    {
                        int nsHour;
                        int nsMinute;
                        if (int.TryParse(nsTimeComponents[0], out nsHour)
                            && int.TryParse(nsTimeComponents[1], out nsMinute))
                        {
                            int index_candidate = nsHour * 2;
                            if (nsMinute == 0)
                                basalIndex = index_candidate;
                            else if (nsMinute == 30)
                                basalIndex = index_candidate + 1;
                        }
                    }
                }

                if (!basalIndex.HasValue)
                    continue;

                if (basalIndex >= 0 && basalIndex < 48 && rate > 0 && rate < 30m)
                    nsRates[basalIndex.Value] = rate;
            }

            return nsRates;
        }
    }
}
