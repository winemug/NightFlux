using System;
using System.Collections.Generic;
using System.Text;

namespace NightFlux.Model
{
    public struct TempBasal : IEntity
    {
        public DateTimeOffset Time;
        public int Duration;
        public decimal? AbsoluteRate;
        public int? Percentage;
    }
}
