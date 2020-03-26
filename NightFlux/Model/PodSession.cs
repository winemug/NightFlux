using System;
using System.Collections.Generic;

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
            Rates.Add((date, null, 10800 * TickUnits, null));
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
        
        public IEnumerable<(DateTimeOffset From, DateTimeOffset To, double Value)> Frames(TimeSpan TimeframeMax)
        {
            var rates = GetInfusionRates();
            using var ratesEnum= rates.GetEnumerator();
            ratesEnum.MoveNext();
            var date = ratesEnum.Current.Key;
            var rate = ratesEnum.Current.Value;
            if (ratesEnum.MoveNext())
            {
                while (true)
                {
                    if (date + TimeframeMax < ratesEnum.Current.Key)
                    {
                        yield return (date, date + TimeframeMax, (double)rate);
                        date = date + TimeframeMax;
                    }
                    else
                    {
                        if (date + TimeframeMax >= ratesEnum.Current.Key)
                        {
                            yield return (date, ratesEnum.Current.Key, (double)rate);
                        }
                        date = ratesEnum.Current.Key;
                        rate = ratesEnum.Current.Value;
                        if (!ratesEnum.MoveNext())
                            break;
                    }
                }
            }
        }
    }
}
