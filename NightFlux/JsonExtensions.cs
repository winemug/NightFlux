using MongoDB.Bson;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NightFlux
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
        public static double? SafePrecisedouble(this JObject jObject, string element, double precision)
        {
            double? ret = null;
            double? dval = jObject.SafeDouble(element);

            if (dval.HasValue)
            {
                ret = dval.Value.ToPreciseDouble(precision);
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
                var valstr = jObject[element].ToString();
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
