using System;
using System.Collections.Generic;
using System.Text;

namespace NightFlux
{
    public struct BasalProfile
    {
        public DateTimeOffset Time;
        public decimal[] BasalRates;
        public int UtcOffsetInMinutes;
    }
}
