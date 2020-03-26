using System;
using System.Collections.Generic;
using System.Linq;

namespace NightFlux.Model
{
    public class PodSession
    {
        public int Id;
        public DateTimeOffset Activated { get; set; }
        public DateTimeOffset Deactivated { get; set; }
        public HormoneType Hormone { get; set; }
        public decimal UnitsPerMilliliter { get; set; }
        public string Name { get; set; }
        public IDictionary<DateTimeOffset, decimal> InfusionRates => GetInfusionRates();

        private IDictionary<DateTimeOffset, decimal> InfusionRatesInternal = null;

        private decimal TickUnits => UnitsPerMilliliter / 2000m;

        private List<(DateTimeOffset Date, decimal? Basal, decimal? Bolus, decimal? ExtendedBolus)> Rates;

        public PodSession()
        {
            Rates = new List<(DateTimeOffset Date, decimal? Basal, decimal? Bolus, decimal? ExtendedBolus)>();
        }

        public PodSession(IDictionary<DateTimeOffset, decimal> infusionRates)
        {
            InfusionRatesInternal = infusionRates;
        }

        public void BasalRate(DateTimeOffset date, int ticksPerHour)
        {
            Rates.Add((date, ticksPerHour * TickUnits, null, null));
        }

        public void ExtendedBolus(DateTimeOffset date, int tickCount, int minutes)
        {
            var ticksPerHour = tickCount * 60m / minutes;
            Rates.Add((date, null, null, ticksPerHour * TickUnits));
            Rates.Add((date.AddMinutes(minutes), null, null, 0));
        }

        public void Bolus(DateTimeOffset date, int tickCount)
        {
            var tickDurationSeconds = tickCount * 2;
            Rates.Add((date, null, 1800 * TickUnits, null));
            Rates.Add((date.AddSeconds(tickDurationSeconds), null, 0, null));
        }

        private IDictionary<DateTimeOffset, decimal> GetInfusionRates()
        {
            if (InfusionRatesInternal == null)
            {
                InfusionRatesInternal = new SortedDictionary<DateTimeOffset, decimal>();

                var basalRate = 0m;
                var bolusRate = 0m;
                var extendedBolusRate = 0m;

                foreach (var ratesEntry in Rates)
                {
                    if (ratesEntry.Basal.HasValue)
                        basalRate = ratesEntry.Basal.Value;

                    if (ratesEntry.Bolus.HasValue)
                        bolusRate = ratesEntry.Bolus.Value;

                    if (ratesEntry.ExtendedBolus.HasValue)
                        extendedBolusRate = ratesEntry.ExtendedBolus.Value;

                    InfusionRatesInternal[ratesEntry.Date] = basalRate + bolusRate + extendedBolusRate;
                }
            }

            return InfusionRatesInternal;
        }
        
        public IEnumerable<(DateTimeOffset From, DateTimeOffset To, double Value)> Frames(TimeSpan window, TimeSpan prolongation)
        {
            var rates = GetInfusionRates();
            var start = rates.First().Key;
            var end = rates.Last().Key + prolongation;
            return WindowedAverages(start, end, window, rates);
        }

        private IEnumerable<(DateTimeOffset start, DateTimeOffset end, double average)>
            WindowedAverages(DateTimeOffset start, DateTimeOffset end, TimeSpan window,
            IDictionary<DateTimeOffset, decimal> rates)
        {
            var tnext = DateTimeOffset.MinValue;
            var rnext = 0d;
            var r = 0d;

            using var re= rates.GetEnumerator();

            var t0 = start;

            while (t0 < end)
            {
                var t1 = t0 + window;

                var windowedTotal = 0d;
                // range t0 to t1
                while (t0 < t1)
                {
                    while (tnext <= t0)
                    {
                        r = rnext;
                        if (re.MoveNext())
                        {
                            tnext = re.Current.Key;
                            rnext = (double) re.Current.Value;
                        }
                        else
                        {
                            tnext = DateTime.MaxValue;
                        }
                    }
                    
                    if (tnext < t1)
                    {
                        windowedTotal += r * (tnext - t0).TotalMilliseconds;
                        t0 = tnext;
                    }
                    else
                    {
                        windowedTotal += r * (t1 - t0).TotalMilliseconds;
                        t0 = t1;
                    }
                }

                yield return (t1 - window, t1, windowedTotal / window.TotalMilliseconds);
            }

        }
    }
}
