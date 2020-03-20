using System;

namespace NightFlux.OmniCoreModel
{
    public struct Infusion
    {
        public DateTimeOffset Time { get; set; }
        public Site Site { get; set; }
        public DeliveryType Type { get; set; }
        public decimal Rate { get; set; }
    }
}
