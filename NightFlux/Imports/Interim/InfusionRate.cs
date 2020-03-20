using System;

namespace NightFlux.Imports.Interim
{
    public struct InfusionRate : INightFluxEntity
    {
        public int SiteId;
        public DateTimeOffset Time;
        public double Rate;
    }
}
