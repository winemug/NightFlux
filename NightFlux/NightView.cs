using NightFlux.View;
using System;
using System.Collections.Generic;
using System.Text;

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
            yield return new TimeValue
            {
                Time = DateTimeOffset.UnixEpoch,
                Value = 0m
            };
        }

        public async IAsyncEnumerable<TimeValue> BasalRates(DateTimeOffset start, DateTimeOffset end)
        {
            using var nsql = await NightSql.GetInstance(Configuration);
            yield return new TimeValue
            {
                Time = DateTimeOffset.UnixEpoch,
                Value = 0m
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
