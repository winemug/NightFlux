using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeZoneConverter;

namespace NightFlux
{
    public class NsExport : IDisposable
    {
        private Configuration Configuration;

        public NsExport(Configuration configuration)
        {
            Configuration = configuration;
        }

        public void Dispose()
        {
        }

        public async IAsyncEnumerable<BgValue> BgEntries(long lastBgTimestamp)
        {
            var mc = new MongoClient(Configuration.NsMongoDbUrl);
            var mdb = mc.GetDatabase(Configuration.NsDbName);

            var entries = mdb.GetCollection<BsonDocument>("entries");
            var filter = new FilterDefinitionBuilder<BsonDocument>()
                .And(
                    new FilterDefinitionBuilder<BsonDocument>().Gt<long>("date", lastBgTimestamp),
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
                        gv = document["sgv"].ToDouble().ToPreciseDecimal(1);
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
        }

        public async IAsyncEnumerable<BasalProfile> ProfileEntries()
        {
            var mc = new MongoClient(Configuration.NsMongoDbUrl);
            var mdb = mc.GetDatabase(Configuration.NsDbName);

            var entries = mdb.GetCollection<BsonDocument>("treatments");

            var filter = new BsonDocument();
            filter.Add("eventType", "Profile Switch");
            filter.Add("$and", new BsonArray()
                    .Add(new BsonDocument()
                            .Add("profileJson", new BsonDocument()
                                    .Add("$exists", new BsonBoolean(true))
                            )
                    )
            );

            var profileSwitches = new List<NsProfileSwitch>();
            using var cursor = await entries.Find(filter).ToCursorAsync();
            while (await cursor.MoveNextAsync())
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    double? ts_val = null;
                    BsonValue bsonVal = null;
                    if (document.TryGetValue("timestamp", out bsonVal) && !bsonVal.IsBsonNull)
                        ts_val = bsonVal.AsNullableDouble;

                    if (document.TryGetValue("NSCLIENT_ID", out bsonVal) && !bsonVal.IsBsonNull)
                        ts_val = bsonVal.AsNullableDouble;

                    if (!ts_val.HasValue)
                        continue;

                    var profileSwitchTime = DateTimeOffset.FromUnixTimeMilliseconds((long)ts_val).UtcDateTime;
                    int percentage = 100;
                    int utcOffset = 0;
                    int timeShift = 0;
                    int duration = 0;

                    if (document.TryGetValue("percentage", out bsonVal) && !bsonVal.IsBsonNull)
                        percentage = bsonVal.AsInt32;

                    if (document.TryGetValue("timeshift", out bsonVal) && !bsonVal.IsBsonNull)
                        timeShift = bsonVal.AsInt32;

                    if (document.TryGetValue("duration", out bsonVal) && !bsonVal.IsBsonNull)
                        duration = bsonVal.AsInt32;

                    var jsonProfile = JObject.Parse(document["profileJson"].AsString);
                    if (jsonProfile.ContainsKey("timezone"))
                    {
                        var tzString = jsonProfile["timezone"].ToString();
                        var tzi = TZConvert.GetTimeZoneInfo(tzString);
                        // including dst at the time of profile switch
                        // as the pod does not switch timezones or dst,
                        // there has to be an explicit profile switch entry for each change in local time
                        utcOffset = (int) tzi.GetUtcOffset(profileSwitchTime).TotalMinutes;
                    }
                    else if (document.TryGetValue("utcOffset", out bsonVal) && !bsonVal.IsBsonNull)
                        utcOffset = bsonVal.AsInt32;

                    var nsBasalRates = new decimal?[48];

                    if (jsonProfile.ContainsKey("basal"))
                    {
                        foreach(JObject nsRate in jsonProfile["basal"])
                        {
                            int? basalIndex = null;
                            decimal? rate = null;

                            double ns_rate;
                            if (nsRate.ContainsKey("value")
                                && double.TryParse(nsRate["value"].ToString(), out ns_rate))
                            {
                                rate = (ns_rate * percentage / 100).ToPreciseDecimal(0.05m);
                            }

                            long ns_tas = 0;
                            if (nsRate.ContainsKey("timeAsSeconds") &&
                                long.TryParse(nsRate["timeAsSeconds"].ToString(), out ns_tas))
                            {
                                basalIndex = (int) ns_tas / 1800;
                            }

                            if (nsRate.ContainsKey("time"))
                            {
                                var nsTimeString = nsRate["time"].ToString();
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
                            }

                            if (basalIndex.HasValue && rate.HasValue
                                && basalIndex >= 0 && basalIndex < 48
                                && rate > 0 && rate < 30m)
                            {
                                nsBasalRates[basalIndex.Value] = rate;
                            }
                        }

                        if (!nsBasalRates[0].HasValue)
                            break;

                        var lastRate = 0m;
                        var rates = new decimal[48];
                        for(int i=0; i<48; i++)
                        {
                            if (nsBasalRates[i].HasValue)
                            {
                                lastRate = nsBasalRates[i].Value;
                            }
                            rates[i] = lastRate;
                        }

                        profileSwitches.Add(new NsProfileSwitch
                        {
                            Time = profileSwitchTime,
                            ProfileOffset = utcOffset + timeShift,
                            Duration = duration == 0 ? null : (int?)duration,
                            Rates = rates
                        });
                    }
                }
            }

            var basalProfiles = new List<BasalProfile>();

            NsProfileSwitch? lastProfileSwitch = null;
            DateTimeOffset? tempSwitchEndTime = null;

            foreach(var profileSwitch in profileSwitches.OrderBy(ps => ps.Time))
            {
                if (lastProfileSwitch.HasValue && tempSwitchEndTime.HasValue
                    && tempSwitchEndTime < profileSwitch.Time)
                {
                    yield return new BasalProfile
                    {
                        Time = tempSwitchEndTime.Value,
                        UtcOffsetInMinutes = lastProfileSwitch.Value.ProfileOffset,
                        BasalRates = lastProfileSwitch.Value.Rates
                    };
                }

                if (!profileSwitch.Duration.HasValue)
                {
                    lastProfileSwitch = profileSwitch;
                    tempSwitchEndTime = null;
                }
                else if (lastProfileSwitch.HasValue)
                {
                    tempSwitchEndTime = profileSwitch.Time.AddMinutes(profileSwitch.Duration.Value);
                }
                else
                {
                    continue;
                }

                yield return new BasalProfile
                {
                    Time = profileSwitch.Time,
                    UtcOffsetInMinutes = profileSwitch.ProfileOffset,
                    BasalRates = profileSwitch.Rates
                };
            }

            if (lastProfileSwitch.HasValue && tempSwitchEndTime.HasValue)
            {
                yield return new BasalProfile
                {
                    Time = tempSwitchEndTime.Value,
                    UtcOffsetInMinutes = lastProfileSwitch.Value.ProfileOffset,
                    BasalRates = lastProfileSwitch.Value.Rates
                };
            }
        }

        public async IAsyncEnumerable<BasalRate> BasalRates(List<BasalProfile> basalProfiles)
        {
            var mc = new MongoClient(Configuration.NsMongoDbUrl);
            var mdb = mc.GetDatabase(Configuration.NsDbName);


            var entries = mdb.GetCollection<BsonDocument>("treatments");
            var filter = new BsonDocument();
            filter.Add("eventType", "Temp Basal");


            var tempBasals = new List<NsTempBasal>();
            using var cursor = await entries.Find(filter).ToCursorAsync();
            while (await cursor.MoveNextAsync())
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    double? ts_val = null;
                    BsonValue bsonVal = null;

                    if (document.TryGetValue("NSCLIENT_ID", out bsonVal) && !bsonVal.IsBsonNull)
                        ts_val = bsonVal.AsNullableDouble;

                    if (!ts_val.HasValue)
                        continue;

                    var tempBasalTime = DateTimeOffset.FromUnixTimeMilliseconds((long)ts_val).UtcDateTime;

                    int? duration = null;
                    decimal? absoluteRate = null;
                    int? percentRate = null;
                    //if (document.TryGetValue("duration", out bsonVal) && !bsonVal.IsBsonNull)
                    //    duration = bsonVal.AsInt32;
                    //if (document.TryGetValue("absolute", out bsonVal) && !bsonVal.IsBsonNull)
                    //    absoluteRate = bsonVal.AsDouble.ToPreciseDecimal(0.05m);
                    //if (document.TryGetValue("rate", out bsonVal) && !bsonVal.IsBsonNull)
                    //    percentRate = bsonVal.AsInt32;
                    tempBasals.Add(new NsTempBasal
                    {
                        Time = tempBasalTime,
                        Duration = duration,
                        AbsoluteRate = absoluteRate,
                        PercentRate = percentRate
                    });
                }
            }

            yield return new BasalRate
            {
                Time = DateTimeOffset.UtcNow,
                Rate = 0
            };

        }

    }

    struct NsProfileSwitch
    {
        public DateTimeOffset Time;
        public int ProfileOffset;
        public int? Duration;
        public decimal[] Rates;
    }

    struct NsTempBasal
    {
        public DateTimeOffset Time;
        public int? Duration;
        public decimal? AbsoluteRate;
        public int? PercentRate;
    }
}
