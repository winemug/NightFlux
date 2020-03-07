using MongoDB.Bson;
using MongoDB.Driver;
using System.Data.SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.Linq;
using MongoDB.Driver.Linq.Expressions;
using MongoDB.Driver.Linq.Processors;
using MongoDB.Driver.Linq.Translators;
using Newtonsoft.Json.Converters;
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
        private long LastTimeStamp;
        private DateTimeOffset LastTimeOffset;

        public NightSync(Configuration configuration)
        {
            MongoClient = new MongoClient(configuration.NsMongoDbUrl);
            MongoDatabase = MongoClient.GetDatabase(configuration.NsDbName);
            Configuration = configuration;
            if (!BsonClassMap.IsClassMapRegistered(typeof(NsTreatment)))
                BsonClassMap.RegisterClassMap<NsTreatment>();
            LastTimeStamp = Configuration.LastSync;
            LastTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(LastTimeStamp);
        }

        public void Dispose()
        {
            NightSql?.Dispose();
        }

        public async Task ImportBg(NightSql nsql)
        {
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
                    double? gv = document.SafeRound("sgv", 1);

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
        }

        public async Task ImportBasalProfiles(NightSql nsql)
        {
            try
            {
                using var cursor = await MongoDatabase.GetCollection<NsTreatment>("treatments")
                    .FindAsync(x => x.eventType == "Profile Switch" && x.profileJson != null
                                                                    && x.created_at > LastTimeOffset.DateTime);


                while (await cursor.MoveNextAsync())
                {
                    foreach (var treatment in cursor.Current)
                    {
                        var basalProfile = await ParseProfileSwitch(treatment);
                        if (basalProfile.HasValue)
                            await nsql.Import(basalProfile.Value);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task ImportTempBasals(NightSql nsql)
        {
            using var cursor = await MongoDatabase.GetCollection<NsTreatment>("treatments")
                .FindAsync(x => x.eventType == "Temp Basal"
                                && x.created_at > LastTimeOffset.DateTime);

            while (await cursor.MoveNextAsync())
            {
                foreach (var treatment in cursor.Current)
                {
                    var tempBasal = await ParseTempBasal(treatment);
                    if (tempBasal.HasValue)
                        await nsql.Import(tempBasal.Value);
                }
            }
        }

        public async Task ImportCarbs(NightSql nsql)
        {
            using var cursor = await MongoDatabase.GetCollection<NsTreatment>("treatments")
                .FindAsync(x => x.carbs.HasValue
                                && x.created_at > LastTimeOffset.DateTime);


            while (await cursor.MoveNextAsync())
            {
                foreach (var treatment in cursor.Current)
                {
                    var carb = await ParseCarbs(treatment);
                    if (carb.HasValue)
                        await nsql.Import(carb.Value);
                }
            }
        }

        public async Task ImportBoluses(NightSql nsql)
        {
            using var cursor = await MongoDatabase.GetCollection<NsTreatment>("treatments")
                .FindAsync(x => (x.insulin.HasValue || x.enteredinsulin.HasValue)
                                && x.created_at > LastTimeOffset.DateTime);

            while (await cursor.MoveNextAsync())
            {
                foreach (var treatment in cursor.Current)
                {
                    var (bolus, extendedBolus) = await ParseExtendedBolus(treatment);
                    if (bolus.HasValue)
                        await nsql.Import(bolus.Value);
                    if (extendedBolus.HasValue)
                        await nsql.Import(extendedBolus.Value);
                }
            }
        }

        private async Task<Carb?> ParseCarbs(NsTreatment treatment)
        {
            var amount = treatment.carbs?.Round(0.1m) ?? 0;

            if (amount <= 0)
                return null;

            return new Carb {
                Time = treatment.EventDate.Value,
                Amount = amount
            };
        }
        private async Task<(Bolus?, ExtendedBolus?)> ParseExtendedBolus(NsTreatment treatment)
        {
            ExtendedBolus? extendedBolus = null;
            Bolus? bolus = null;

            var duration = treatment.duration ?? 0;
            var amount = treatment.enteredinsulin?.Round(0.05m) ?? 0;
            var splitExt = treatment.splitExt ?? 0;
            var splitNow = treatment.splitNow ?? 0;

            var amountExt = (amount * splitExt / 100.0).Round(0.05m);
            if (amountExt > 0)
                extendedBolus = new ExtendedBolus {
                    Time = treatment.EventDate.Value,
                    Duration = (int)duration,
                    Amount = amount
                };

            var amountNow = (amount * splitNow / 100.0).Round(0.05m);
            if (amountNow > 0)
                bolus = new Bolus {
                    Time = treatment.EventDate.Value,
                    Amount = amount
                };

            return (bolus, extendedBolus);
        }

        private async Task<TempBasal?> ParseTempBasal(NsTreatment treatment)
        {
            var duration = treatment.duration ?? 0;
            double? absoluteRate = treatment.absolute?.Round(0.05m);
            var percentage = treatment.percent ?? 0;

            return new TempBasal {
                Time = treatment.EventDate.Value,
                Duration = (int)duration,
                AbsoluteRate = absoluteRate,
                Percentage = percentage
            };
        }

        private async Task<BasalProfile?> ParseProfileSwitch(NsTreatment treatment)
        {
            BasalProfile? ret = null;

            var profileSwitchTime = treatment.EventDate.Value;
            var joProfile = JObject.Parse(treatment.profileJson);

            try
            {
                int percentage = treatment.percentage ?? 100;
                int timeShift = treatment.timeshift ?? 0;
                int duration = (int) (treatment.duration ?? 0);

                int utcOffset;
                if (joProfile.ContainsKey("timezone"))
                {
                    var tzString = joProfile["timezone"].ToString();
                    var tzi = TZConvert.GetTimeZoneInfo(tzString);
                    // including dst at the time of profile switch
                    // as the pod does not switch timezones or dst,
                    // there has to be an explicit profile switch entry for each change in local time
                    utcOffset = (int) tzi.GetUtcOffset(profileSwitchTime).TotalMinutes;
                }
                else
                    utcOffset = treatment.utcOffset ?? 0;

                var rates = await GetBasalRates(joProfile, percentage);
                if (rates != null)
                {
                    ret = new BasalProfile()
                    {
                        Time = profileSwitchTime,
                        BasalRates = rates,
                        UtcOffsetInMinutes = utcOffset + timeShift,
                        Duration = duration
                    };
                }
            }
            catch { }
            return ret;
        }

        private async Task<double[]> GetBasalRates(JObject joProfile, int percentage)
        {
            var rates = new double[48];
            var nsRates = await GetSingleRates(joProfile, percentage);

            if (!nsRates[0].HasValue)
                return null;

            var lastRate = 0.0;
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

        private async Task<double?[]> GetSingleRates(JObject joProfile, int percentage)
        {
            var nsRates = new double?[48];

            if (!joProfile.ContainsKey("basal"))
                return nsRates;

            foreach(JObject joRate in joProfile["basal"])
            {
                int? basalIndex = null;

                var nsRate = joRate.SafeDouble("value");
                if (!nsRate.HasValue)
                    continue;

                var rate = (nsRate.Value * percentage / 100.0).Round(0.05m);

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

                if (basalIndex >= 0 && basalIndex < 48 && rate > 0 && rate < 30)
                    nsRates[basalIndex.Value] = rate;
            }

            return nsRates;
        }
    }
}
