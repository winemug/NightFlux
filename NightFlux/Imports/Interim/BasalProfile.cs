using System;

namespace NightFlux.Imports.Interim
{
    public struct BasalProfile : INightFluxEntity
    {
        public DateTimeOffset Time;
        public double[] BasalRates;
        public int UtcOffsetInMinutes;
        public int Duration;
    }
}
