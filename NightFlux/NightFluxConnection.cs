using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using Microsoft.Data.Sqlite;
using NightFlux.Data;
using NightFlux.Imports;
using NightFlux.Imports.Interim;
using NightFlux.Model;
using Nito.AsyncEx;

namespace NightFlux
{
    public class NightFluxConnection : IDisposable
    {
        private readonly Configuration Configuration;
        private readonly InfluxDBClient InfluxClient;
        private readonly WriteApi InfluxWriteApi;

        private static NightFluxConnection Instance;
        private static object InstanceLock = new object();
        
        public static NightFluxConnection GetInstance(Configuration configuration)
        {
            lock (InstanceLock)
            {
                if (Instance == null)
                {
                    Instance = new NightFluxConnection(configuration);
                }
                return Instance;
            }
        }
        
        private NightFluxConnection(Configuration configuration)
        {
            Configuration = configuration;
            InfluxClient = InfluxDBClientFactory.Create(Configuration.InfluxUrl, Configuration.InfluxToken.ToCharArray());
            var wo = WriteOptions.CreateNew()
                .BatchSize(8192)
                .WriteScheduler(new NewThreadScheduler())
                .RetryInterval(750)
                .JitterInterval(136)
                .FlushInterval(1000)
                .Build();
            InfluxWriteApi = InfluxClient.GetWriteApi(wo);
        }

        public async Task RunSync()
        {
            await RecreateBucket();
            await SyncNightscoutBg();
            await SyncOmniCoreExports();
        }
        
        private async Task RecreateBucket()
        {
            var bucketApi = InfluxClient.GetBucketsApi();
            var orgApi = InfluxClient.GetOrganizationsApi();
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
       
        public void Dispose()
        {
            InfluxClient?.Dispose();
        }

        private async Task SyncNightscoutBg()
        {
            using var nsr = new NightscoutReader(Configuration);
           
            await foreach (var pd in nsr.BgValues(DateTimeOffset.MinValue, DateTimeOffset.MaxValue))
            {
                //InfluxWriteApi.WritePoint(Configuration.InfluxBucket, Configuration.InfluxOrgId, pd);
            }
        }

        private async Task SyncOmniCoreExports()
        {
            var tsvReader = new TsvReader(Configuration);
            // InfluxWriteApi.WritePoints(Configuration.InfluxBucket, Configuration.InfluxOrgId, pds);
        }
    }
}