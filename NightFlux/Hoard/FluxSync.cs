//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Globalization;
//using System.IO;
//using System.Linq;
//using System.Reactive.Concurrency;
//using System.Text;
//using System.Threading.Tasks;
//using InfluxDB.Client;
//using InfluxDB.Client.Api.Domain;
//using InfluxDB.Client.Writes;
//using MongoDB.Bson;
//using MongoDB.Driver;
//using NightFlux.OmniCoreModel;
//using TimeValue = System.Collections.Generic.KeyValuePair<System.DateTimeOffset, double?>;

//namespace NightFlux
//{
//    public class FluxSync
//    {
//        private Configuration Configuration;
//        public FluxSync(Configuration configuration)
//        {
//            Configuration = configuration;
//        }

//        public async Task Cleanup()
//        {
//            var client = InfluxDBClientFactory.Create(Configuration.InfluxUrl, Configuration.InfluxToken.ToCharArray());
//            var bucketApi = client.GetBucketsApi();

//            var orgApi = client.GetOrganizationsApi();
//            var organization = (await orgApi.FindOrganizationsAsync()).First();


//            var bucket = await bucketApi.FindBucketByNameAsync(Configuration.InfluxBucket);

//            if (bucket?.Name == Configuration.InfluxBucket)
//            {
//                await bucketApi.DeleteBucketAsync(bucket);
//                //var deleteApi = client.GetDeleteApi();
//                //await deleteApi.Delete(DateTime.MinValue, DateTime.MaxValue, "true",
//                //    bucket, organization);
//            }
//            await bucketApi.CreateBucketAsync(Configuration.InfluxBucket, organization);
//            client.Dispose();
//        }

//        public async Task Import()
//        {
//            var mc = new MongoClient(Configuration.NsMongoDbUrl);
//            var mdb = mc.GetDatabase(Configuration.NsDbName);

//            var lastTimestamp = DateTimeOffset.UtcNow.AddDays(-90).ToUnixTimeMilliseconds();
//            var entries = mdb.GetCollection<BsonDocument>("entries");
//            var filter = new FilterDefinitionBuilder<BsonDocument>()
//                .And(
//                    new FilterDefinitionBuilder<BsonDocument>().Gt<long>("date", lastTimestamp),
//                    new FilterDefinitionBuilder<BsonDocument>().Eq<string>("type", "sgv")
//                );

//            using var cursor = await entries.Find(filter).ToCursorAsync();

//            var client = InfluxDBClientFactory.Create(Configuration.InfluxUrl, Configuration.InfluxToken.ToCharArray());
//            var wo = WriteOptions.CreateNew()
//                .BatchSize(1024)
//                .WriteScheduler(NewThreadScheduler.Default)
//                //.RetryInterval(3000)
//                //.FlushInterval(500)
//                //.JitterInterval(280)
//                .Build();

//            var start = DateTimeOffset.UtcNow.AddYears(-1);
//            var end = DateTimeOffset.UtcNow.AddHours(24);
//            var writeApi = client.GetWriteApi(wo);
//            {
//                // import bg from ns database
//                while (await cursor.MoveNextAsync())
//                {
//                    foreach (BsonDocument document in cursor.Current)
//                    {
//                        DateTimeOffset? dt = document.SafeDateTimeOffset("date");
//                        double? gv = document.SafeRound("sgv", 1);

//                        if (dt.HasValue && gv.HasValue)
//                        {
//                            var point = PointData
//                                .Measurement("luk")
//                                .Field("bgc", gv.Value)
//                                .Timestamp(dt.Value, WritePrecision.Ms);

//                            writeApi.WritePoint(Configuration.InfluxBucket, Configuration.InfluxOrgId, point);
//                        }
//                    }
//                }

//                // import files

//                var folder = Configuration.OmniCoreExportsFolder;
//                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
//                    return;

//                var importTasks = new List<Task>();
//                foreach(var filePath in Directory.GetFiles(folder))
//                {
//                    importTasks.Add(
//                        Task.Run(async () =>
//                        {
//                            var ps = await ReadPodSession(filePath);
//                            await ps.WriteValues(writeApi, Configuration);
//                        }));
//                }

//                await Task.WhenAll(importTasks);

//                writeApi.Flush();
//            }
//            //writeApi.Dispose();
//            //client.Dispose();
//        }

//        public async Task<PodSession> ReadPodSession(string path)
//        {
//            using var sr = new StreamReader(path);

//            var year = 1;
//            var month = 1;
//            var day = 1;
//            var utcOffset = 60;
//            string hormone = "insulin";
//            decimal solution = 100;
//            decimal baseRate = 0;
//            decimal currentRate = 0;
//            decimal? tbRate = null;
//            int defaultTbDuration = 30;
//            DateTimeOffset tbEnd = new DateTimeOffset();

//            PodSession ps = null;

//            string site = "";

//            string line = null;
//            try
//            {
//                while (!sr.EndOfStream)
//                {
//                    line = await sr.ReadLineAsync();
//                    if (string.IsNullOrEmpty(line?.Trim()))
//                        continue;

//                    line = line.Replace(' ', '\t');

//                    if (line.StartsWith('#'))
//                    {
//                        year = int.Parse(line[1..5]);
//                        month = int.Parse(line[5..7]);
//                        day = int.Parse(line[7..9]);
//                        //if (line.Length > 12)
//                        //    utcOffset = int.Parse(line[12..]);
//                        continue;
//                    }

//                    var cols = line.Split('\t');

//                    var hour = int.Parse(cols[0][0..2]);
//                    var minute = int.Parse(cols[0][2..4]);
//                    var second = 0;
//                    if (cols[0].Length > 4)
//                        second = int.Parse(cols[0][4..5]);

//                    var entryDate = new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.FromMinutes(utcOffset));
//                    var entryType = cols[1];

//                    if (tbRate.HasValue && tbEnd > entryDate)
//                    {
//                        currentRate = baseRate;
//                        tbRate = null;
//                        ps.BasalRate(entryDate, (int) (currentRate / 0.05m));
//                    }

//                    switch (entryType)
//                    {
//                        case "s":
//                            hormone = cols[2];
//                            solution = decimal.Parse(cols[3], CultureInfo.InvariantCulture);
//                            baseRate = decimal.Parse(cols[4], CultureInfo.InvariantCulture);
//                            site = $"{entryDate:yyMMdd-hhmm}-{hormone}-{solution}";
//                            currentRate = baseRate;

//                            ps = new PodSession
//                            {
//                                Hormone = hormone == "insulin" ? HormoneType.Insulin : HormoneType.Glucagon,
//                                UnitsPerMilliliter = solution,
//                                Name = site
//                            };

//                            ps.BasalRate(entryDate, (int) (currentRate / 0.05m));


//                            break;
//                        case "x":
//                            currentRate = decimal.Parse(cols[2], CultureInfo.InvariantCulture);
//                            ps.BasalRate(entryDate, (int) (currentRate / 0.05m));
//                            break;
//                        case "z":
//                        case "b":
//                            var bolusAmount = decimal.Parse(cols[2], CultureInfo.InvariantCulture);
//                            if (cols.Length > 3 && cols[3].Trim().Length > 0)
//                            {
//                                var duration = int.Parse(cols[3]);
//                                ps.ExtendedBolus(entryDate, (int) (bolusAmount / 0.05m), duration);
//                            }
//                            else
//                            {
//                                ps.Bolus(entryDate, (int) (bolusAmount / 0.05m));
//                            }

//                            break;
//                        case "tb":
//                            tbRate = decimal.Parse(cols[2], CultureInfo.InvariantCulture);
//                            var tbDuration = defaultTbDuration;
//                            if (cols.Length > 3 && cols[3].Trim().Length > 0)
//                            {
//                                tbDuration = int.Parse(cols[3]);
//                            }

//                            currentRate = tbRate.Value;
//                            ps.BasalRate(entryDate, (int) (currentRate / 0.05m));
//                            tbEnd = entryDate.AddMinutes(tbDuration);
//                            break;
//                        case "tbc":
//                            currentRate = baseRate;
//                            tbRate = null;
//                            ps.BasalRate(entryDate, (int) (currentRate / 0.05m));
//                            break;
//                        case "e":
//                            ps.BasalRate(entryDate, (int) 0);
//                            break;
//                    }
//                }
//            }
//            catch
//            {
//                Debug.Write($"Offending line: {line}");
//                throw;
//            }

//            return ps;
//        }

//        public async Task Export(NightView nv)
//        {
//            var client = InfluxDBClientFactory.Create(Configuration.InfluxUrl, Configuration.InfluxToken.ToCharArray());
//            var wo = WriteOptions.CreateNew()
//                .BatchSize(1024)
//                //.WriteScheduler(NewThreadScheduler.Default)
//                .Build();

//            var start = DateTimeOffset.UtcNow.AddDays(-14);
//            var end = DateTimeOffset.UtcNow.AddHours(24);
//            using (var writeApi = client.GetWriteApi(wo))
//            {
//                foreach (var gv in await nv.GlucoseValues(start, end))
//                {
//                    var point = PointData
//                        .Measurement("luki")
//                        .Field("bgc", gv.Value)
//                        .Timestamp(gv.Time, WritePrecision.Ms);

//                    writeApi.WritePoint(Configuration.InfluxBucket, Configuration.InfluxOrgId, point);
//                }

//                //await foreach (var gv in nv.BasalTicks(start, end))
//                //{
//                //    if (gv.Value.HasValue)
//                //    {
//                //        var point = PointData
//                //            .Measurement("luki")
//                //            .Field("insulin", gv.Value.Value)
//                //            .Tag("type", "basal")
//                //            .Timestamp(gv.Key, WritePrecision.Ms);

//                //        writeApi.WritePoint(Configuration.InfluxBucket, Configuration.InfluxOrgId, point);

//                //    }
//                //}

//                //await foreach (var gv in nv.ExtendedBolusTicks(start, end))
//                //{
//                //    if (gv.Value.HasValue)
//                //    {
//                //        var point = PointData
//                //            .Measurement("luki")
//                //            .Field("insulin", gv.Value.Value)
//                //            .Tag("type", "extendedbolus")
//                //            .Timestamp(gv.Key, WritePrecision.Ms);

//                //        writeApi.WritePoint(Configuration.InfluxBucket, Configuration.InfluxOrgId, point);

//                //    }
//                //}

//                //await foreach (var gv in nv.BolusTicks(start, end))
//                //{
//                //    if (gv.Value.HasValue)
//                //    {
//                //        var point = PointData
//                //            .Measurement("luki")
//                //            .Field("insulin", gv.Value.Value)
//                //            .Tag("type", "bolus")
//                //            .Timestamp(gv.Key, WritePrecision.Ms);

//                //        writeApi.WritePoint(Configuration.InfluxBucket, Configuration.InfluxOrgId, point);

//                //    }
//                //}

//                //await foreach (var bv in nv.BasalRates(start, end))
//                //{
//                //    if (bv.Value.HasValue)
//                //    {
//                //        var point = PointData
//                //            .Measurement("luki")
//                //            .Field("basal_rate", bv.Value.Value)
//                //            .Timestamp(bv.Key, WritePrecision.Ms);

//                //        writeApi.WritePoint(Configuration.InfluxBucket, Configuration.InfluxOrgId, point);
//                //    }
//                //}
//                writeApi.Flush();
//            }
//        }
//    }
//}
