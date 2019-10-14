using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using InfluxData.Net.InfluxDb.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NightFlux
{
    public class FluxImport : IDisposable
    {
        private InfluxDbClient Client;
        private string BucketName;
        private ConcurrentQueue<Point> Points;
        private Task Uploader;
        private TaskCompletionSource<bool> FinalizeImport;
        private const int UploadBatchSize = 256;

        public FluxImport(IConfigurationSection cs)
        {
            Client = new InfluxDbClient(cs["url"], "", "", InfluxDbVersion.v_1_3);
            BucketName = cs["bucket"];
            FinalizeImport = new TaskCompletionSource<bool>();
            Points = new ConcurrentQueue<Point>();
            Uploader = Task.Run(async () => await BatchUpload());
        }

        public void Dispose()
        {
            FinalizeImport.TrySetResult(true);
            Uploader.GetAwaiter().GetResult();
        }

        public void QueueImport(BgValue bgv)
        {
            Points.Enqueue(
                new Point {
                    Name = "bg",
                    Fields = new Dictionary<string, object> {{"value", bgv.Value }},
                    Timestamp = bgv.Time.UtcDateTime
                    });
        }

        private async Task BatchUpload()
        {
            Task waitResult = null;
            Point point = null;

            var payload = new List<Point>();
            int batchTotal = 0;

            while(waitResult != FinalizeImport.Task || Points.Count > 0)
            {
                while (Points.TryDequeue(out point))
                {
                    payload.Add(point);
                    if (++batchTotal >= UploadBatchSize)
                        break;
                }

                if (batchTotal > 0)
                {
                    var response = await Client.Client.WriteAsync(payload, BucketName);
                    if (!response.Success)
                        throw new Exception($"Influxdb reports error while writing: {response.StatusCode} {response.Body}" );
                    batchTotal = 0;
                    payload = new List<Point>();
                }

                waitResult = await Task.WhenAny(FinalizeImport.Task, Task.Delay(2000));
            }
        }
    }
}
