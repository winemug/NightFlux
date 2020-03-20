using System.Globalization;
using Newtonsoft.Json.Linq;

namespace NightFlux.Helpers
{
    public static class JsonExtensions
    {

        public static double? SafeDouble(this JObject jObject, string element)
        {
            double? ret = null;

            if (jObject.ContainsKey(element))
            {
                double dval;
                if (double.TryParse(jObject[element].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out dval))
                {
                    ret = dval;
                }
            }
            return ret;
        }
        public static double? SafeRound(this JObject jObject, string element, decimal precision)
        {
            double? ret = null;
            double? dval = jObject.SafeDouble(element);

            if (dval.HasValue)
            {
                ret = dval.Value.Round(precision);
            }
            return ret;
        }

        public static string SafeString(this JObject jObject, string element)
        {
            string ret = null;
            if (jObject.ContainsKey(element))
            {
                ret = jObject[element].ToString();
            }
            return ret;
        }

        public static int? SafeInt(this JObject jObject, string element)
        {
            int? ret = null;
            if (jObject.ContainsKey(element))
            {
                int ival;
                if (int.TryParse(jObject[element].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ival))
                {
                    ret = ival;
                }
            }
            return ret;
        }
    }
}
