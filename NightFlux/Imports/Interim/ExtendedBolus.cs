using System;

namespace NightFlux.Imports.Interim
{
    public struct ExtendedBolus : INightFluxEntity
    {
        public DateTimeOffset Time;
        public double? Amount;
        public int Duration;

        public double CalculatedRate
        {
            get
            {
                if (!Amount.HasValue || Duration == 0)
                    return 0;
                return Amount.Value * 60.0 / Duration;
            }
        }
    }
}
