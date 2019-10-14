using InfluxDB.LineProtocol.Client;
using InfluxDB.LineProtocol.Payload;
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
        private LineProtocolClient InfluxDbClient;
        private ConcurrentQueue<LineProtocolPoint> Points;
        private Task Uploader;
        private TaskCompletionSource<bool> FinalizeImport;
        private const int UploadBatchSize = 256;

        public FluxImport(IConfigurationSection cs)
        {
            InfluxDbClient = new LineProtocolClient(new Uri(cs["url"]), cs["db"], cs["username"], cs["password"]);
            FinalizeImport = new TaskCompletionSource<bool>();
            Points = new ConcurrentQueue<LineProtocolPoint>();
            Uploader = Task.Run(async () => await BatchUpload());
        }

        public void Dispose()
        {
            FinalizeImport.TrySetResult(true);
            Uploader.GetAwaiter().GetResult();
        }

        public void QueueImport(BgValue bgv)
        {
            Points.Enqueue(new LineProtocolPoint("bg",
                new Dictionary<string, object>
                {
                    { "value", bgv.Value },
                },
                null,
                bgv.Time.UtcDateTime));
        }

        private async Task BatchUpload()
        {
            Task waitResult = null;
            LineProtocolPoint point = null;

            var payload = new LineProtocolPayload();
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
                    var influxResult = await InfluxDbClient.WriteAsync(payload);
                    if (!influxResult.Success)
                    {
                        throw new Exception($"Upload to influxdb failed with error message: {influxResult.ErrorMessage}");
                    }
                    batchTotal = 0;
                    payload = new LineProtocolPayload();
                }

                waitResult = await Task.WhenAny(FinalizeImport.Task, Task.Delay(2000));
            }
        }
    }
}
