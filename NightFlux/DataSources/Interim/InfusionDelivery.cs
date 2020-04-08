using System;

namespace NightFlux.Imports.Interim
{
    public struct InfusionDelivery : INightFluxEntity
    {
        public int SiteId;
        public DateTimeOffset Time;
        public double Delivered;
    }
}
