using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Deedle;
using Deedle.Vectors;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.FSharp.Core;
using TimeValue = System.Collections.Generic.KeyValuePair<System.DateTimeOffset, double?>;

namespace NightFlux.Experiments
{
    public class Simulator
    {
        private NightView Nv;

        public Simulator(NightView nv)
        {
            Nv = nv;
        }

        public async Task<List<TimeValue>> GetWindowedInsulinState(DateTimeOffset start, DateTimeOffset end,
            TimeSpan window)
        {
            var list = new List<TimeValue>();
            var sc = new InjectionSite();

            var windowDictionary = new Dictionary<DateTimeOffset, double>();

            var t = start;
            while (t < end)
            {
                windowDictionary.Add(t, 0d);
                t += window;
            }

            await foreach (var bv in Nv.BasalTicks(start, end))
            {
                if (bv.Value.HasValue)
                {
                    sc.AdvanceToDate(bv.Key);
                    sc.Inject(bv.Value.Value);
                }
            }

            return list;
        }

    }
}
