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

        public async IAsyncEnumerable<GlucoseValue> GlucoseValues(DateTimeOffset start, DateTimeOffset end)
        {
            using var nsql = await NightSql.GetInstance(Configuration);
            var sql = "SELECT time, value FROM bg WHERE time >= @t1 AND time < @t2";
            var parameters = new [] { nsql.GetParameter("t1", start), nsql.GetParameter("t2", end) };
            await foreach(var dr in nsql.ExecuteQuery(sql, parameters))
            {
                yield return new GlucoseValue
                {
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(dr.GetInt64(0)),
                    Value = dr.GetDecimal(1)
                };
            }
        }
    }
}
