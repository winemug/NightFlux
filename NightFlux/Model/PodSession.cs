using System;
using System.Collections.Generic;
using System.Linq;

namespace NightFlux.Model
{
    public class PodSession
    {
        public int Id { get; set; }
        public DateTimeOffset Activated { get; set; }
        public DateTimeOffset Deactivated { get; set; } = DateTimeOffset.MaxValue;
        public HormoneType Hormone { get; set; }
        public decimal Dilution { get; set; }
        public string Name { get; set; }
        public string Composition { get; set; }

        private SortedDictionary<DateTimeOffset,(decimal? BasalRate, decimal? ExtendedBolusRate, decimal? Bolus)> ChannelDictionary;

        public PodSession()
        {
            ChannelDictionary =
                new SortedDictionary<DateTimeOffset, (decimal? BasalRate, decimal? ExtendedBolusRate, decimal? Bolus)>();
        }

        public void SetBasalRate(DateTimeOffset date, decimal basalRate)
        {
            while (ChannelDictionary.ContainsKey(date))
            {
                date = date.AddSeconds(1);
            }

            ChannelDictionary.Add(date, (basalRate, null, null));
        }

        public void ExtendedBolus(DateTimeOffset date, decimal totalAmount, int totalMinutes)
        {
            var ebRate = totalAmount * 60m / totalMinutes;
            while (ChannelDictionary.ContainsKey(date))
            {
                date = date.AddSeconds(1);
            }
            ChannelDictionary.Add(date, (null, ebRate, null));

            ChannelDictionary.Add(date.AddMinutes(totalMinutes), (null, 0, null));
        }

        public void Bolus(DateTimeOffset date, decimal totalAmount)
        {
            while (ChannelDictionary.ContainsKey(date))
            {
                date = date.AddSeconds(1);
            }
            ChannelDictionary.Add(date.AddSeconds((double) (2 * (totalAmount / 0.05m))),
                (null, null, totalAmount));
        }

        public List<(DateTimeOffset Time, decimal Value)> GetRates()
        {
            var list = new List<(DateTimeOffset Time, decimal Rate)>();

            var basalRate = 0m;
            var extendedBolusRate = 0m;

            list.Add((Activated, 0m));
            var lastEntry = Activated;

            var nowEntry = DateTimeOffset.UtcNow;
            if (Deactivated == DateTimeOffset.MaxValue)
            {
                ChannelDictionary.Add(nowEntry, (null, null, null));
            }
            
            foreach (var channelItem in ChannelDictionary)
            {
                var entryDate = channelItem.Key;


                var entry = channelItem.Value;
                
                if (entry.BasalRate.HasValue)
                    basalRate = entry.BasalRate.Value;
                
                if (entry.ExtendedBolusRate.HasValue)
                    extendedBolusRate = entry.ExtendedBolusRate.Value;
                
                list.Add((entryDate, basalRate + extendedBolusRate));
            }
            
            if (Deactivated == DateTimeOffset.MaxValue)
            {
                ChannelDictionary.Remove(nowEntry);
            }
            return list;
        }

        public List<(DateTimeOffset Time, double Delivered)> GetDeliveries()
        {
            var list = new List<(DateTimeOffset Time, double Delivered)>();

            var deposit = 0d;
            var basalRate = 0m;
            var extendedBolusRate = 0m;

            list.Add((Activated, 0d));
            var lastEntry = Activated;

            var nowEntry = DateTimeOffset.UtcNow;
            if (Deactivated == DateTimeOffset.MaxValue)
            {
                ChannelDictionary.Add(nowEntry, (null, null, null));
            }
            
            foreach (var channelItem in ChannelDictionary)
            {
                var entryDate = channelItem.Key;

                var timePast = entryDate - lastEntry;
                if (timePast.TotalSeconds > 0)
                {
                    deposit += (double)basalRate * timePast.TotalHours;
                    deposit += (double)extendedBolusRate * timePast.TotalHours;
                }

                var entry = channelItem.Value;
                
                if (entry.BasalRate.HasValue)
                    basalRate = entry.BasalRate.Value;
                
                if (entry.ExtendedBolusRate.HasValue)
                    extendedBolusRate = entry.ExtendedBolusRate.Value;

                if (entry.Bolus.HasValue)
                    deposit += (double) entry.Bolus.Value;
                
                list.Add((entryDate, deposit));
            }
            
            if (Deactivated == DateTimeOffset.MaxValue)
            {
                ChannelDictionary.Remove(nowEntry);
            }
            return list;
        }
       
    }
}
