using Newtonsoft.Json;
using NightFlux.Model;
using NightFlux.View;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;
using System.Threading.Tasks;

namespace NightFlux
{
    public class NightView
    {
        private Configuration Configuration;

        public NightView(Configuration configuration)
        {
            Configuration = configuration;
        }

        public async IAsyncEnumerable<TimeValue> GlucoseValues(DateTimeOffset start, DateTimeOffset end)
        {
            using var nsql = await NightSql.GetInstance(Configuration);
            var sql = "SELECT time, value FROM bg WHERE time >= @t1 AND time < @t2";
            var parameters = new [] { nsql.GetParameter("t1", start), nsql.GetParameter("t2", end) };
            await foreach(var dr in nsql.ExecuteQuery(sql, parameters))
            {
                yield return new TimeValue
                {
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(dr.GetInt64(0)),
                    Value = dr.GetDecimal(1)
                };
            }
        }

        public async IAsyncEnumerable<TimeValue> CarbEntries(DateTimeOffset start, DateTimeOffset end)
        {
            using var nsql = await NightSql.GetInstance(Configuration);
            yield return new TimeValue
            {
                Time = DateTimeOffset.UnixEpoch,
                Value = 0m
            };
        }

        public async IAsyncEnumerable<TimeValue> BolusEntries(DateTimeOffset start, DateTimeOffset end)
        {
            using var nsql = await NightSql.GetInstance(Configuration);
            var sql = "SELECT time, amount FROM bolus WHERE time >= @t1 AND time < @t2";
            var parameters = new [] { nsql.GetParameter("t1", start), nsql.GetParameter("t2", end) };
            await foreach(var dr in nsql.ExecuteQuery(sql, parameters))
            {
                yield return new TimeValue
                {
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(dr.GetInt64(0)),
                    Value = dr.GetDecimal(1)
                };
            }
        }

        public async IAsyncEnumerable<TimeValue> BasalRates(DateTimeOffset start, DateTimeOffset end)
        {
            using var nsql = await NightSql.GetInstance(Configuration);
            var sql = "SELECT time FROM basal WHERE duration = 0 AND time <= @t LIMIT 1";
            var profile_start = (decimal?) await nsql.ExecuteScalar(sql, new [] { nsql.GetParameter("t", start)});
            if (profile_start.HasValue)
            {

                sql = "SELECT * FROM basal WHERE time >= @t1 AND time < @t2";
                var parameters = new [] { nsql.GetParameter("t1", profile_start.Value), nsql.GetParameter("t2", end) };

                BasalProfile? lastProfile = null;
                await foreach(var profileReader in nsql.ExecuteQuery(sql, parameters))
                {
                    var profile = await ReadProfile(profileReader);
                    if (!lastProfile.HasValue && profile.Duration == 0)
                        lastProfile = profile;


                    yield return new TimeValue
                    {
                        Time = DateTimeOffset.UnixEpoch,
                        Value = 0m
                    };
                }
            }
        }

        private async Task<BasalProfile> ReadProfile(SQLiteDataReader dr)
        {
            return new BasalProfile
            {
                Time = DateTimeOffset.FromUnixTimeMilliseconds(dr.GetInt64(0)),
                Duration = dr.GetInt32(1),
                UtcOffsetInMinutes = dr.GetInt32(2),
                BasalRates = JsonConvert.DeserializeObject<decimal[]>(dr.GetString(3))
            };
        }

        public async IAsyncEnumerable<TimeValue> ExtendedBasalRates(DateTimeOffset start, DateTimeOffset end)
        {
            using var nsql = await NightSql.GetInstance(Configuration);
            yield return new TimeValue
            {
                Time = DateTimeOffset.UnixEpoch,
                Value = 0m
            };
        }

    }
}
