namespace NightFlux.Helpers
{
    public static class FloatingPointExtensions
    {
        public static double Round(this double val, decimal precision)
        {
            return (double) val.ToDecimal(precision);
        }

        public static decimal ToDecimal(this double val, decimal precision)
        {
            var ret = (decimal)val;
            var remainder = ret % precision;
            var midpoint = precision / 2m;
            if (remainder < midpoint)
                ret -= remainder;
            else
                ret += precision - remainder;

            return ret;
        }

        public static bool IsSameAs(this double val, double otherValue, decimal precision)
        {
            return val.ToDecimal(precision) == otherValue.ToDecimal(precision);
        }
    }
}
