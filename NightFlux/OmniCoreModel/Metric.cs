using System;

namespace NightFlux.OmniCoreModel
{
    public struct Metric
    {
        public DateTimeOffset Time { get; set; }
        public MetricType Type { get; set; }
        public decimal Value { get; set; }
    }
}
