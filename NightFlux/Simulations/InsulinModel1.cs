using System;
using System.Collections.Generic;

namespace NightFlux.Simulations
{
    public class InsulinModel1
    {
        private void Simulation1(SortedDictionary<DateTimeOffset, decimal> rates)
        {
            TimeSpan maxOffset = TimeSpan.FromMinutes(5);

            rates[DateTimeOffset.Now.AddHours(12)] = 0m;

            var ratesEnum = rates.GetEnumerator();
            ratesEnum.MoveNext();
            var date = ratesEnum.Current.Key;
            var rate = ratesEnum.Current.Value;
            if (!ratesEnum.MoveNext())
                return;

            while (true)
            {
                if (date + maxOffset < ratesEnum.Current.Key)
                {
                    DoSimulate1(date, date + maxOffset, (double)rate);
                    date = date + maxOffset;
                }
                else
                {
                    if (date + maxOffset > ratesEnum.Current.Key)
                    {
                        DoSimulate1(date, ratesEnum.Current.Key, (double)rate);
                    }
                    date = ratesEnum.Current.Key;
                    rate = ratesEnum.Current.Value;
                    if (!ratesEnum.MoveNext())
                        return;
                }
            }
        }

        private double q1a = 0, q1b = 0, q2 = 0, q3 = 0;

        private double hexamerRatio = 0.7;

        private double ka1 = 0.0112;
        private double ka2 = 0.0210;
        private double kElim = 0.0368;

        private double vmaxld = 0.4201;
        private double kmld = 5.5;

        private void DoSimulate1(DateTimeOffset from, DateTimeOffset to, double hourlyRate)
        {
            var duration = to - from;

            var dq1a = hourlyRate * duration.TotalHours * hexamerRatio;
            dq1a -= q1a * ka1 * duration.TotalMinutes;
            var ld1a = vmaxld * q1a * duration.TotalMinutes / (kmld + q1a);
            dq1a -= ld1a;

            var dq1b = hourlyRate * duration.TotalHours * (1 - hexamerRatio);
            dq1b -= q1b * ka2 * duration.TotalMinutes;
            var ld1b = vmaxld * q1b * duration.TotalMinutes / (kmld + q1b);
            dq1b -= ld1b;

            var dq2 = q1a * ka1 * duration.TotalMinutes;
            dq2 -= q2 * ka1;

            var dq3 = q2 * ka1 * duration.TotalMinutes;
            dq3 += q1b * ka2 * duration.TotalMinutes;
            dq3 -= q3 * kElim;

            q1a += dq1a;
            q1b += dq1b;
            q2 += dq2;
            q3 += dq3;

            if (q1a < 0)
                q1a = 0;

            if (q1b < 0)
                q1b = 0;

            if (q2 < 0)
                q2 = 0;

            if (q3 < 0)
                q3 = 0;
        }
    }
}
