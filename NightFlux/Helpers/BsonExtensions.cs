using System;
using MongoDB.Bson;
using Newtonsoft.Json.Linq;

namespace NightFlux.Helpers
{
    public static class BsonExtensions
    {
        public static DateTimeOffset? SafeDateTimeOffset(this BsonDocument bsonDocument, string element)
        {
            DateTimeOffset? ret = null;
            BsonValue bsonValue;
            if (bsonDocument.TryGetValue(element, out bsonValue))
            {
                if (bsonValue != null && !bsonValue.IsBsonNull)
                {
                    if (bsonValue.IsInt64)
                    {
                        try
                        {
                            ret = DateTimeOffset.FromUnixTimeMilliseconds(bsonValue.AsInt64).UtcDateTime;
                        }
                        catch { }
                    }
                    else if (bsonValue.IsDouble)
                    {
                        try
                        {
                            ret = DateTimeOffset.FromUnixTimeMilliseconds((long)bsonValue.AsDouble).UtcDateTime;
                        }
                        catch { }
                    }
                    else if (bsonValue.IsString)
                    {
                        try
                        {
                            ret = DateTimeOffset.FromUnixTimeMilliseconds(bsonValue.AsBsonDateTime
                                .MillisecondsSinceEpoch);
                        }
                        catch { }
                    }
                }
            }
            return ret;
        }

        public static double? SafeRound(this BsonDocument bsonDocument, string element, decimal precision)
        {
            double? ret = null;
            BsonValue bsonValue;
            if (bsonDocument.TryGetValue(element, out bsonValue))
            {
                if (bsonValue != null && !bsonValue.IsBsonNull)
                {
                    try
                    {
                        if (bsonValue.IsDouble)
                            ret = bsonValue.AsDouble.Round(precision);
                        else if (bsonValue.IsInt32)
                            ret = bsonValue.AsInt32;
                        else if (bsonValue.IsInt64)
                            ret = bsonValue.AsInt64;
                    }
                    catch { }
                }
            }
            return ret;
        }

        public static JObject SafeJsonObject(this BsonDocument bsonDocument, string element)
        {
            JObject ret = null;
            BsonValue bsonValue;
            if (bsonDocument.TryGetValue(element, out bsonValue))
            {
                if (bsonValue != null && !bsonValue.IsBsonNull && bsonValue.IsString)
                {
                    try
                    {
                        ret = JObject.Parse(bsonValue.AsString);
                    }
                    catch { }
                }
            }
            return ret;
        }

        public static int? SafeInt(this BsonDocument bsonDocument, string element)
        {
            int? ret = null;
            BsonValue bsonValue;
            if (bsonDocument.TryGetValue(element, out bsonValue))
            {
                if (bsonValue != null && !bsonValue.IsBsonNull && bsonValue.IsInt32)
                {
                    try
                    {
                        ret = bsonValue.AsInt32;
                    }
                    catch { }
                }
            }
            return ret;
        }

        public static string SafeString(this BsonDocument bsonDocument, string element)
        {
            string ret = null;
            BsonValue bsonValue;
            if (bsonDocument.TryGetValue(element, out bsonValue))
            {
                if (bsonValue != null && !bsonValue.IsBsonNull && bsonValue.IsString)
                {
                    try
                    {
                        ret = bsonValue.AsString;
                    }
                    catch { }
                }
            }
            return ret;
        }
    }
}
