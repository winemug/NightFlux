﻿using System;
using System.Collections.Generic;
using System.Text;

namespace NightFlux.Model
{
    public struct ExtendedBolus : IEntity
    {
        public DateTimeOffset Time;
        public decimal? Amount;
        public int Duration;
    }
}