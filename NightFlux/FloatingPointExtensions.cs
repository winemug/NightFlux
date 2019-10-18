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
            var result = Convert.ToDecimal(val);
            var remainder = result % precision;
            var midpoint = precision / 2m;
            if (remainder < midpoint)
                result -= remainder;
            else
                result += precision - remainder;

            Debug.WriteLine($"{val} {precision} {result}");
            return result;
        }
    }
}
