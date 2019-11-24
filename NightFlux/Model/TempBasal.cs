using System;
using System.Collections.Generic;
using System.Text;

namespace NightFlux.Model
{
    public struct TempBasal : IEntity
    {
        public DateTimeOffset Time;
        public int Duration;
        public double? AbsoluteRate;
        public int? Percentage;
    }
}
