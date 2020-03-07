using System;
using System.Collections.Generic;
using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NightFlux.Model
{
    public class NsTreatment
    {
        public ObjectId Id { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.String)]
        public DateTime created_at { get; set; }

        //[BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        //public DateTime? timestamp { get; set; }
        public double? NSCLIENT_ID { get; set; }

        public string eventType { get; set; }
        public string profileJson { get; set; }
        public double? duration { get; set; }
        public double? absolute { get; set; }
        public int? percent { get; set; }
        public int? percentage { get; set; }
        public int? timeshift { get; set; }
        public int? utcOffset { get; set; }
        public double? insulin { get; set; }
        public double? carbs { get; set; }
        public double? enteredinsulin { get; set; }
        public int? splitExt { get; set; }
        public int? splitNow { get; set; }

        [BsonExtraElements]
        public BsonDocument CatchAll { get; set; }

        public DateTimeOffset? EventDate
        {
            get
            {
                if (NSCLIENT_ID.HasValue)
                    return DateTimeOffset.FromUnixTimeMilliseconds((long) (NSCLIENT_ID.Value));
                else
                    return new DateTimeOffset(created_at);
            }
        }
    }
}
