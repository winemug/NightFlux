﻿using System;
using System.Collections.Generic;
using System.Text;

namespace NightFlux.Model
{
    public struct BasalProfile : IEntity
    {
        public DateTimeOffset Time;
        public decimal[] BasalRates;
        public int UtcOffsetInMinutes;
        public int Duration;
    }
}