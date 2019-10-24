﻿using System;
using System.Collections.Generic;
using System.Text;

namespace NightFlux.Model
{
    public struct Carb : IEntity
    {
        public DateTimeOffset Time;
        public decimal Amount;
        public string ImportId;
    }
}