using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NightFlux
{
    public static class FloatingPointExtensions
    {
        public static double ToPreciseDouble(this double val, double precision)
        {
            var remainder = val % precision;
            var midpoint = precision / 2.0;
            if (remainder < midpoint)
                val -= remainder;
            else
                val += precision - remainder;

            return val;
        }

    }
}
