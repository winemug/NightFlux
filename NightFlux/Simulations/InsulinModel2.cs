using System;
using System.Collections.Generic;
using System.Linq;
using NightFlux.Model;

namespace NightFlux.Simulations
{
    public class InsulinModel2
    {
        public static IEnumerable<(DateTimeOffset Date, double Value)> Run(PodSession podSession, TimeSpan settleDown)
        {
            var deliveries = podSession.GetRates();

            var re = 0d;

            var t0 = deliveries[0].Time;
            var r0 = (double)deliveries[0].Value;


            foreach (var delivery in deliveries.Skip(1))
            {
                var tx = t0;

                var t1 = delivery.Time;
                var r1 = (double)delivery.Value;

                double tp, rx;
                while (tx < t1)
                {
                    tp = (tx - t0).TotalSeconds / settleDown.TotalSeconds;
                    if (tp > 1)
                        tp = 1;
                    tp *= Math.Sin(Math.PI / 2 * tp);
                    rx = (r0 - re) * tp + re;

                    yield return (tx, rx);

                    tx = tx.AddMinutes(1);
                }

                tp = (tx - t0).TotalSeconds / settleDown.TotalSeconds;
                if (tp > 1)
                    tp = 1;
                tp *= Math.Sin(Math.PI /2 * tp);
                re = (r0 - re) * tp + re;

                t0 = t1;
                r0 = r1;
            }
        }
    }
}