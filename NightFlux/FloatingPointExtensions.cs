using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NightFlux
{
    public static class FloatingPointExtensions
    {
        public static decimal ToPreciseDecimal(this double val, decimal precision)
        {
            return Convert.ToDecimal(val).ToPreciseDecimal(precision);
        }

        public static decimal ToPreciseDecimal(this decimal val, decimal precision)
        {
            var remainder = val % precision;
            var midpoint = precision / 2m;
            if (remainder < midpoint)
                val -= remainder;
            else
                val += precision - remainder;
            return val;
        }

    }
}
