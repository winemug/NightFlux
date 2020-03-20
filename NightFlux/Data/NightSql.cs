using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using NightFlux.Helpers;
using NightFlux.Imports.Interim;
using NightFlux.Model;
using Nito.AsyncEx;

namespace NightFlux.Data
{
    public class NightSql
    {
        private readonly Configuration Configuration;
        private SqliteConnection Connection;
        private ConcurrentQueue<INightFluxEntity> BatchEntities;

        private static AsyncLock SqlInitLock = new AsyncLock();
        private static NightSql Instance;

        private NightSql(Configuration configuration)
        {
            Configuration = configuration;
        }

        public static async Task<NightSql> GetInstance(Configuration configuration)
        {
            var initLock = await SqlInitLock.LockAsync();
            if (Instance == null)
            {
                Instance = new NightSql(configuration);
                await Instance.Initialize();
            }
            initLock.Dispose();
            return Instance;
        }

        public Task Flush()
        {
            return EmptyQueue();
        }

        private async Task Initialize()
        {
            var connStr = new SqliteConnectionStringBuilder() {DataSource = Configuration.SqlitePath}.ConnectionString;
            Connection = new SqliteConnection(connStr);
            await Connection.OpenAsync();

            await Connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS bg" +
                                  "(time INTEGER NOT NULL PRIMARY KEY, value REAL);");
            await Connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_time1 ON bg(time);");

            await Connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS basal" +
                                  "(time INTEGER NOT NULL PRIMARY KEY, utc_offset INTEGER, duration INTEGER, rates TEXT);");
            //await ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_time2 ON basal(time);");
            
            await Connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS tempbasal" +
                                  "(time INTEGER NOT NULL PRIMARY KEY, duration INTEGER, absolute REAL, percentage INTEGER);");
            //await ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_time3 ON tempbasal(time);");

            await Connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS bolus" +
                                  "(time INTEGER NOT NULL PRIMARY KEY, amount REAL);");
            //await ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_time4 ON bolus(time);");

            await Connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS extended_bolus" +
                                  "(time INTEGER NOT NULL PRIMARY KEY, amount REAL, duration INTEGER);");
            //await ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_time5 ON extended_bolus(time);");

            await Connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS carb" +
                                  "(time INTEGER NOT NULL PRIMARY KEY, amount REAL);");
            //await ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_time6 ON carb(time);");

            await Connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS oc_site" +
                                          "(id INTEGER NOT NULL PRIMARY KEY, name TEXT, hormone INTEGER, units REAL, start INTEGER, stop INTEGER);");
            await Connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_name0 ON oc_site(name);");

            await Connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS oc_metric" +
                                          "(time INTEGER NOT NULL PRIMARY KEY, type INTEGER, value REAL);");
            //await Connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_type0 ON oc_metric(type);");

            await Connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS oc_infusion" +
                                          "(time INTEGER NOT NULL PRIMARY KEY, site_id INTEGER, rate REAL);");

            await Connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_site0 ON oc_infusion(site_id);");

            BatchEntities = new ConcurrentQueue<INightFluxEntity>();
        }

        private async Task EmptyQueue()
        {
            var transaction = await Connection.BeginTransactionAsync();
            while (BatchEntities.TryDequeue(out var iv))
            {
                await InsertEntity(iv, transaction);
            }

            await transaction.CommitAsync();
        }

        public async Task ImportEntity(INightFluxEntity record)
        {
            BatchEntities.Enqueue(record);
            if (BatchEntities.Count >= 1024)
                await EmptyQueue();
        }

        public async Task ImportOmniCorePodSession(PodSession podSession)
        {
            var transaction = await Connection.BeginTransactionAsync();
            var id = await Connection.ExecuteScalarAsync<int?>(
                "SELECT id FROM oc_site WHERE name = @A",
                new
                {
                    A = podSession.Name,
                }, transaction);

            if (id != null)
            {
                await Connection.ExecuteAsync("DELETE FROM oc_infusion WHERE site_id = @A", new {A = id.Value}, transaction);
                await Connection.ExecuteAsync("DELETE FROM oc_site WHERE id = @A", new {A = id.Value}, transaction);
            }

            await Connection.ExecuteAsync("INSERT INTO oc_site(name,hormone,units,start,stop) VALUES(@A,@B,@C,@D,@E)",
                new
                {
                    A = podSession.Name,
                    B = (int)podSession.Hormone,
                    C = podSession.UnitsPerMilliliter,
                    D = podSession.Activated.ToUnixTimeMilliseconds(),
                    E = podSession.Deactivated.ToUnixTimeMilliseconds()
                }, transaction);

            id = await Connection.ExecuteScalarAsync<int>("SELECT last_insert_rowid();");
            await transaction.CommitAsync();
            foreach (var rate in podSession.InfusionRates)
            {
                await ImportEntity(new InfusionRate() { Rate = (double)rate.Value, Time = rate.Key, SiteId = id.Value });
            }
        }

        //public async Task RemoveDuplicateCarbs()
        //{
        //    using var conn = await GetConnection();
        //    var duplicateIds = new List<long>();
        //    await foreach (var dr in ExecuteQuery("SELECT import_id, COUNT(*) FROM carb GROUP BY import_id HAVING COUNT(*) > 1", null, conn))
        //    {
        //        var importId = dr.GetString(0);
        //        var duplicateCount = dr.GetInt64(1) - 1;
        //        await foreach (var rowInfo in ExecuteQuery("SELECT rowid FROM carb WHERE import_id = @i ORDER BY time DESC LIMIT @k",
        //            new [] { GetParameter("i", importId), GetParameter("k", duplicateCount) }, conn))
        //        {
        //            duplicateIds.Add(rowInfo.GetInt64(0));
        //        }
        //    }

        //    using var tran = await conn.BeginTransactionAsync();
        //    foreach(var duplicateId in duplicateIds)
        //    {
        //        await ExecuteNonQuery("DELETE FROM carb WHERE rowid = @d",
        //            new [] { GetParameter("d", duplicateId) }, conn);
        //    }
        //    await tran.CommitAsync();
        //}

        private async Task InsertEntity(INightFluxEntity iv, IDbTransaction transaction)
        {
            try
            {
                if (iv is BgValue bgValue)
                {
                    await Connection.ExecuteAsync("INSERT OR REPLACE INTO bg(time,value) VALUES(@A,@B)", new
                    {
                        A = bgValue.Time.ToUnixTimeMilliseconds(),
                        B = bgValue.Value
                    }, transaction);
                }
                //else if (iv is BasalProfile)
                //{
                //    await ImportProfile((BasalProfile) iv);
                //}
                //else if (iv is TempBasal)
                //{
                //    await ImportTempBasal((TempBasal) iv);
                //}
                //else if (iv is Bolus)
                //{
                //    await ImportBolus((Bolus) iv);
                //}
                //else if (iv is Carb)
                //{
                //    await ImportCarb((Carb) iv);
                //}
                //else if (iv is ExtendedBolus)
                //{
                //    await ImportExtendedBolus((ExtendedBolus) iv);
                //}
                else if (iv is InfusionRate infusionRate)
                {
                    await Connection.ExecuteAsync("INSERT OR REPLACE INTO oc_infusion(time, site_id, rate) VALUES(@A,@B,@C)", new
                    {
                        A = infusionRate.Time.ToUnixTimeMilliseconds(),
                        B = infusionRate.SiteId,
                        C = infusionRate.Rate
                    }, transaction);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error importing entity {iv}\n{e}");
            }
        }

        // private async Task ImportProfile(BasalProfile basalProfile)
        // {
        //     await Connection.ExecuteAsync("INSERT OR REPLACE INTO basal(time,utc_offset,duration,rates) VALUES(@A,@B,@C,@D)",
        //         new
        //         {
        //             A = basalProfile.Time.ToUnixTimeMilliseconds(),
        //             B = basalProfile.UtcOffsetInMinutes,
        //             C = basalProfile.Duration,
        //             D = JsonConvert.SerializeObject(basalProfile.BasalRates)
        //         });
        // }
        //
        // private async Task ImportTempBasal(TempBasal tempBasal)
        // {
        //     await Connection.ExecuteAsync("INSERT OR REPLACE INTO tempbasal(time,duration,absolute,percentage) VALUES(@A,@B,@C,@D)",
        //         new 
        //         {
        //             A = tempBasal.Time.ToUnixTimeMilliseconds(),
        //             B = tempBasal.Duration,
        //             C = tempBasal.AbsoluteRate,
        //             D = tempBasal.Percentage
        //         });
        // }
        //
        // private async Task ImportBolus(Bolus bolus)
        // {
        //     await Connection.ExecuteAsync("INSERT OR REPLACE INTO bolus(time,amount) VALUES(@A,@B)",
        //         new
        //         { A = bolus.Time.ToUnixTimeMilliseconds(), B = bolus.Amount });
        // }
        //
        // private async Task ImportExtendedBolus(ExtendedBolus extendedBolus)
        // {
        //     await Connection.ExecuteAsync(
        //         "INSERT OR REPLACE INTO extended_bolus(time,amount,duration) VALUES(@A,@B,@C)",
        //         new
        //         {
        //             A = extendedBolus.Time.ToUnixTimeMilliseconds(),
        //             B = extendedBolus.Amount,
        //             C = extendedBolus.Duration
        //         });
        // }
        //
        // private async Task ImportCarb(Carb carb)
        // {
        //     await Connection.ExecuteAsync("INSERT OR REPLACE INTO carb(time,amount) VALUES(@A,@B)",
        //         new
        //         {
        //             A = carb.Time.ToUnixTimeMilliseconds(),
        //             B = carb.Amount
        //         });
        // }

        public async Task<long> GetLastBgDate()
        {
            return await Connection.ExecuteScalarAsync<long>("SELECT time FROM bg ORDER BY time DESC LIMIT 1");
        }

        public async Task<IEnumerable<BgValue>> BgValues(DateTimeOffset start, DateTimeOffset end)
        {
            var v = await Connection.QueryAsync("SELECT time, value FROM bg WHERE time >= @A AND time < @B ORDER BY time",
                new {A = start.ToUnixTimeMilliseconds(), B = end.ToUnixTimeMilliseconds()});

            return v.Select(x => new BgValue { Time = DateTimeOffset.FromUnixTimeMilliseconds(x.time), Value = x.value });
        }

        public async Task<IEnumerable<PodSession>> PodSessions(DateTimeOffset start, DateTimeOffset end)
        {
            var rs = await Connection.QueryAsync("SELECT id,name,hormone,units,start,stop FROM oc_site WHERE (start >= @A AND start < @B)" +
                                                         " OR (start < @A AND stop > @A)",
                new {A = start.ToUnixTimeMilliseconds(), B = end.ToUnixTimeMilliseconds()});

            var sessions = new List<PodSession>();
            foreach (var r in rs)
            {
                var rates = await Connection.QueryAsync(
                    "SELECT time, rate FROM oc_infusion WHERE site_id = @A ORDER BY time ASC",
                    new {A = (int)r.id});

                var d = new SortedDictionary<DateTimeOffset, decimal>();
                foreach(var rate in rates)
                {
                    d[DateTimeOffset.FromUnixTimeMilliseconds(rate.time)] = ((double)rate.rate).ToDecimal(0.001m);
                }

                sessions.Add(new PodSession(d)
                {
                    Activated = DateTimeOffset.FromUnixTimeMilliseconds(r.start),
                    Deactivated = DateTimeOffset.FromUnixTimeMilliseconds(r.stop),
                    Hormone = (HormoneType) r.hormone,
                    Name = r.name,
                    UnitsPerMilliliter = ((double) r.units).ToDecimal(1m)
                });
            }

            return sessions;
        }
    }
}
