using System;
using System.Collections.Generic;
using System.Text;

namespace NightFlux.Model
{
    public struct BgValue : IEntity
    {
        public DateTimeOffset Time;
        public double Value;
    }
}
