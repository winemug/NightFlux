using System;
using System.Collections.Generic;
using System.Text;

namespace NightFlux.Model
{
    public struct Bolus : IEntity
    {
        public DateTimeOffset Time;
        public decimal Amount;
    }
}
