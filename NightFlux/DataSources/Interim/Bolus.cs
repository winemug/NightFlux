using System;

namespace NightFlux.Imports.Interim
{
    public struct Bolus : INightFluxEntity
    {
        public DateTimeOffset Time;
        public double Amount;
    }
}
