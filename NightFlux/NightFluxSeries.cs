using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Deedle;

namespace NightFlux
{
    public static class NightFluxSeries
    {
        public static Series<DateTimeOffset, double> GetSeries(DateTimeOffset start, DateTimeOffset end)
        {
            var configuration = Configuration.Load();
            var nightView = new NightView(configuration);

            var builder = new SeriesBuilder<DateTimeOffset, double>();
            Task.Run(async () =>
            {
                await foreach (var br in nightView.BasalTicks(start, end))
                {
                    if (br.Value.HasValue)
                        builder.Add(br.Key, br.Value.Value);
                }

                await foreach (var br in nightView.BolusTicks(start, end))
                {
                    if (br.Value.HasValue)
                        builder.Add(br.Key, br.Value.Value);
                }
            }).Wait();
            return builder.Series;
        }

        public static Frame<DateTimeOffset, string> InsulinFrames(DateTimeOffset start, DateTimeOffset end)
        {
            var configuration = Configuration.Load();
            var nightView = new NightView(configuration);

            var allTicks = new List<KeyValuePair<DateTimeOffset, double?>>();

            Task.Run(async () =>
            {
                await foreach (var br in nightView.BasalTicks(start, end))
                {
                    allTicks.Add(br);
                }

                await foreach (var br in nightView.BolusTicks(start, end))
                {
                    allTicks.Add(br);
                }
            }).Wait();

            return
                Frame.FromValues(allTicks, pair => "insulin", pair => pair.Key, pair => pair.Value);
        }
    }
}
