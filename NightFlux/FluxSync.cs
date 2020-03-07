using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using TimeValue = System.Collections.Generic.KeyValuePair<System.DateTimeOffset, double?>;

namespace NightFlux
{
    public class FluxSync
    {
        private Configuration Configuration;
        public FluxSync(Configuration configuration)
        {
            Configuration = configuration;
        }

        public async Task Cleanup()
        {
            var client = InfluxDBClientFactory.Create(Configuration.InfluxUrl, Configuration.InfluxToken.ToCharArray());
            var bucketApi = client.GetBucketsApi();

            var orgApi = client.GetOrganizationsApi();
            var organization = (await orgApi.FindOrganizationsAsync()).First();


            var bucket = await bucketApi.FindBucketByNameAsync(Configuration.InfluxBucket);

            if (bucket?.Name == Configuration.InfluxBucket)
            {
                await bucketApi.DeleteBucketAsync(bucket);
                //var deleteApi = client.GetDeleteApi();
                //await deleteApi.Delete(DateTime.MinValue, DateTime.MaxValue, "true",
                //    bucket, organization);
            }
            await bucketApi.CreateBucketAsync(Configuration.InfluxBucket, organization);
        }

        public async Task Export(NightView nv)
        {
            var client = InfluxDBClientFactory.Create(Configuration.InfluxUrl, Configuration.InfluxToken.ToCharArray());
            var wo = WriteOptions.CreateNew()
                .BatchSize(1024)
                //.WriteScheduler(NewThreadScheduler.Default)
                .Build();

            var start = DateTimeOffset.UtcNow.AddDays(-14);
            var end = DateTimeOffset.UtcNow.AddHours(24);
            using (var writeApi = client.GetWriteApi(wo))
            {
                await foreach (var gv in nv.GlucoseValues(start, end))
                {
                    if (gv.Value.HasValue)
                    {
                        var point = PointData
                            .Measurement("luki")
                            .Field("bgc", gv.Value.Value)
                            .Timestamp(gv.Key, WritePrecision.Ms);

                        writeApi.WritePoint(Configuration.InfluxBucket, Configuration.InfluxOrgId, point);
                    }
                }

                await foreach (var gv in nv.BasalTicks(start, end))
                {
                    if (gv.Value.HasValue)
                    {
                        var point = PointData
                            .Measurement("luki")
                            .Field("insulin", gv.Value.Value)
                            .Tag("type", "basal")
                            .Timestamp(gv.Key, WritePrecision.Ms);

                        writeApi.WritePoint(Configuration.InfluxBucket, Configuration.InfluxOrgId, point);

                    }
                }

                await foreach (var gv in nv.ExtendedBolusTicks(start, end))
                {
                    if (gv.Value.HasValue)
                    {
                        var point = PointData
                            .Measurement("luki")
                            .Field("insulin", gv.Value.Value)
                            .Tag("type", "extendedbolus")
                            .Timestamp(gv.Key, WritePrecision.Ms);

                        writeApi.WritePoint(Configuration.InfluxBucket, Configuration.InfluxOrgId, point);

                    }
                }

                await foreach (var gv in nv.BolusTicks(start, end))
                {
                    if (gv.Value.HasValue)
                    {
                        var point = PointData
                            .Measurement("luki")
                            .Field("insulin", gv.Value.Value)
                            .Tag("type", "bolus")
                            .Timestamp(gv.Key, WritePrecision.Ms);

                        writeApi.WritePoint(Configuration.InfluxBucket, Configuration.InfluxOrgId, point);

                    }
                }

                //await foreach (var bv in nv.BasalRates(start, end))
                //{
                //    if (bv.Value.HasValue)
                //    {
                //        var point = PointData
                //            .Measurement("luki")
                //            .Field("basal_rate", bv.Value.Value)
                //            .Timestamp(bv.Key, WritePrecision.Ms);

                //        writeApi.WritePoint(Configuration.InfluxBucket, Configuration.InfluxOrgId, point);
                //    }
                //}
                writeApi.Flush();
            }
        }

        public List<TimeValue> InsulinTable(TimeValue bolus)
        {
            var table = new List<TimeValue>();

            var iobTime = bolus.Key;
            var endTime = bolus.Key.AddHours(6);
            var bolusAmount = bolus.Value.Value;

            while (iobTime < endTime)
            {
                iobTime.AddMinutes(1);
            }

            return table;
        }
    }
}
