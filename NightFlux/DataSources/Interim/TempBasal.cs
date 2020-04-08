using System;

namespace NightFlux.Imports.Interim
{
    public struct TempBasal : INightFluxEntity
    {
        public DateTimeOffset Time;
        public int Duration;
        public double? AbsoluteRate;
        public int? Percentage;
    }
}
