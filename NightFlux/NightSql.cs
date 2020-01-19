using Newtonsoft.Json;
using NightFlux.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.SQLite;
using System.Text;
using System.Threading.Tasks;

namespace NightFlux
{
    public class NightSql : IDisposable
    {
        private string SqliteConnectionString;

        private ConcurrentQueue<IEntity> BatchEntities;
        private TaskCompletionSource<bool> StopQueueSource;
        private Task QueueProcessorTask;

        private NightSql(Configuration configuration)
        {
            SqliteConnectionString = $"Data Source={configuration.SqlitePath};Version=3;Pooling=True";
        }

        public static async Task<NightSql> GetInstance(Configuration configuration)
        {
            var instance = new NightSql(configuration);
            await instance.Initialize();
            return instance;
        }

        public void Dispose()
        {
        }

        public async Task StartBatchImport()
        {
            BatchEntities = new ConcurrentQueue<IEntity>();
            StopQueueSource = new TaskCompletionSource<bool>();
            QueueProcessorTask = Task.Run(async () => await QueueProcessor());
        }

        private async Task QueueProcessor()
        {
            while (true)
            {
                var waitResult = await Task.WhenAny(StopQueueSource.Task, Task.Delay(TimeSpan.FromMilliseconds(500)));
                if (waitResult == StopQueueSource.Task)
                    break;
                if (BatchEntities.Count > 1023)
                {
                    await EmptyQueue();
                }
            }
            await EmptyQueue();
        }

        private async Task EmptyQueue()
        {
            await using var conn = await GetConnection();
            await using var tran = await conn.BeginTransactionAsync();
            while (BatchEntities.TryDequeue(out var iv))
            {
                await InsertEntity(iv, conn);
            }
            await tran.CommitAsync();
        }

        public async Task Import(IEntity record)
        {
            if (BatchEntities == null)
            {
                InsertEntity(record);
            }
            else
            {
                BatchEntities.Enqueue(record);
            }
        }

        public async Task FinalizeBatchImport()
        {
            StopQueueSource.SetResult(true);
            await QueueProcessorTask;
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

        private async Task InsertEntity(IEntity iv, SQLiteConnection conn = null)
        {
            try
            {
                if (iv is BgValue)
                {
                    await ImportBG((BgValue)iv, conn);
                }
                else if (iv is BasalProfile)
                {
                    await ImportProfile((BasalProfile) iv, conn);
                }
                else if (iv is TempBasal)
                {
                    await ImportTempBasal((TempBasal) iv, conn);
                }
                else if (iv is Bolus)
                {
                    await ImportBolus((Bolus) iv, conn);
                }
                else if (iv is Carb)
                {
                    await ImportCarb((Carb) iv, conn);
                }
                else if (iv is ExtendedBolus)
                {
                    await ImportExtendedBolus((ExtendedBolus) iv, conn);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error importing entity {iv}\n{e}");
            }
        }

        private async Task ImportBG(BgValue bgValue, SQLiteConnection conn)
        {
            await ExecuteNonQuery("INSERT OR REPLACE INTO bg(time,value) VALUES(@t, @v)",
                new[]
                {
                    GetParameter("t", bgValue.Time),
                    GetParameter("v", bgValue.Value)
                }, conn);
            //var r = (long) await ExecuteScalar("SELECT COUNT(*) FROM bg WHERE time > @t1 AND time < @t2", new []
            //{
            //    GetParameter("t1", bgValue.Time.AddSeconds(-1)),
            //    GetParameter("t2", bgValue.Time.AddSeconds(1)),
            //});

            //if (r == 0)
            //{

            //}
        }

        private async Task ImportProfile(BasalProfile basalProfile, SQLiteConnection conn)
        {
            await ExecuteNonQuery("INSERT OR REPLACE INTO basal(time,utc_offset,duration,rates) VALUES(@t, @u, @d, @r)",
                new []
                {
                    GetParameter("t", basalProfile.Time),
                    GetParameter("u", basalProfile.UtcOffsetInMinutes),
                    GetParameter("d", basalProfile.Duration),
                    GetParameter("r", JsonConvert.SerializeObject(basalProfile.BasalRates))
                }, conn);
            //var r = (long) await ExecuteScalar("SELECT COUNT(*) FROM bg WHERE time > @t1 AND time < @t2", new []
            //{
            //    GetParameter("t1", basalProfile.Time.AddSeconds(-1)),
            //    GetParameter("t2", basalProfile.Time.AddSeconds(1)),
            //});

            //if (r == 0)
        }

        private async Task ImportTempBasal(TempBasal tempBasal, SQLiteConnection conn)
        {
            await ExecuteNonQuery("INSERT OR REPLACE INTO tempbasal(time,duration,absolute,percentage) VALUES(@t, @d, @u, @p)",
                new []
                {
                    GetParameter("t", tempBasal.Time),
                    GetParameter("d", tempBasal.Duration),
                    GetParameter("u", tempBasal.AbsoluteRate),
                    GetParameter("p", tempBasal.Percentage)
                }, conn);
            //var r = (long) await ExecuteScalar("SELECT COUNT(*) FROM bg WHERE time > @t1 AND time < @t2", new []
            //{
            //    GetParameter("t1", tempBasal.Time.AddSeconds(-1)),
            //    GetParameter("t2", tempBasal.Time.AddSeconds(1)),
            //});

            //if (r == 0)
        }

        private async Task ImportBolus(Bolus bolus, SQLiteConnection conn)
        {
            await ExecuteNonQuery("INSERT OR REPLACE INTO bolus(time,amount) VALUES(@t, @a)",
                new []
                {
                    GetParameter("t", bolus.Time),
                    GetParameter("a", bolus.Amount)
                }, conn);
            //var r = (long) await ExecuteScalar("SELECT COUNT(*) FROM bg WHERE time > @t1 AND time < @t2", new []
            //{
            //    GetParameter("t1", bolus.Time.AddSeconds(-1)),
            //    GetParameter("t2", bolus.Time.AddSeconds(1)),
            //});

            //if (r == 0)
            //    await ExecuteNonQuery("INSERT OR REPLACE INTO bolus(time,amount) VALUES(@t, @a)",
            //    new []
            //    {
            //        GetParameter("t", bolus.Time),
            //        GetParameter("a", bolus.Amount)
            //    }, conn);
        }

        private async Task ImportExtendedBolus(ExtendedBolus extendedBolus, SQLiteConnection conn)
        {
            await ExecuteNonQuery("INSERT OR REPLACE INTO extended_bolus(time,amount,duration) VALUES(@t, @a, @d)",
                new []
                {
                    GetParameter("t", extendedBolus.Time),
                    GetParameter("a", extendedBolus.Amount),
                    GetParameter("d", extendedBolus.Duration)
                }, conn);
            //var r = (long) await ExecuteScalar("SELECT COUNT(*) FROM bg WHERE time > @t1 AND time < @t2", new []
            //{
            //    GetParameter("t1", extendedBolus.Time.AddSeconds(-1)),
            //    GetParameter("t2", extendedBolus.Time.AddSeconds(1)),
            //});

            //if (r == 0)
            //    await ExecuteNonQuery("INSERT OR REPLACE INTO extended_bolus(time,amount,duration) VALUES(@t, @a, @d)",
            //    new []
            //    {
            //        GetParameter("t", extendedBolus.Time),
            //        GetParameter("a", extendedBolus.Amount),
            //        GetParameter("d", extendedBolus.Duration)
            //    }, conn);
        }

        private async Task ImportCarb(Carb carb, SQLiteConnection conn)
        {
            await ExecuteNonQuery("INSERT OR REPLACE INTO carb(time,amount) VALUES(@t, @a)",
                new []
                {
                    GetParameter("t", carb.Time),
                    GetParameter("a", carb.Amount),
                }, conn);
            //var r = (long) await ExecuteScalar("SELECT COUNT(*) FROM bg WHERE time > @t1 AND time < @t2", new []
            //{
            //    GetParameter("t1", carb.Time.AddSeconds(-60)),
            //    GetParameter("t2", carb.Time.AddSeconds(60)),
            //});

            //if (r == 0)
            //    await ExecuteNonQuery("INSERT OR REPLACE INTO carb(time,amount) VALUES(@t, @a)",
            //    new []
            //    {
            //        GetParameter("t", carb.Time),
            //        GetParameter("a", carb.Amount),
            //    }, conn);
        }

        public async Task<long> GetLastBgDate()
        {
            await foreach (var dr in ExecuteQuery("SELECT time FROM bg ORDER BY time DESC LIMIT 1"))
            {
                return dr.GetInt64(0);
            }
            return 0;
        }

        private async Task Initialize()
        {
            await ExecuteNonQuery("CREATE TABLE IF NOT EXISTS bg" +
                "(time INTEGER PRIMARY KEY, value REAL);");
            //await ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_time1 ON bg(time);");

            await ExecuteNonQuery("CREATE TABLE IF NOT EXISTS basal" +
                "(time INTEGER PRIMARY KEY, utc_offset INTEGER, duration INTEGER, rates TEXT);");
            //await ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_time2 ON basal(time);");
            
            await ExecuteNonQuery("CREATE TABLE IF NOT EXISTS tempbasal" +
                "(time INTEGER PRIMARY KEY, duration INTEGER, absolute REAL, percentage INTEGER);");
            //await ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_time3 ON tempbasal(time);");

            await ExecuteNonQuery("CREATE TABLE IF NOT EXISTS bolus" +
                "(time INTEGER PRIMARY KEY, amount REAL);");
            //await ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_time4 ON bolus(time);");

            await ExecuteNonQuery("CREATE TABLE IF NOT EXISTS extended_bolus" +
                "(time INTEGER PRIMARY KEY, amount REAL, duration INTEGER);");
            //await ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_time5 ON extended_bolus(time);");

            await ExecuteNonQuery("CREATE TABLE IF NOT EXISTS carb" +
                "(time INTEGER PRIMARY KEY, amount REAL);");
            //await ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_time6 ON carb(time);");
        }

        public async Task<SQLiteConnection> GetConnection()
        {
            var conn = new SQLiteConnection(SqliteConnectionString);
            await conn.OpenAsync();
            return conn;
        }

        public async IAsyncEnumerable<SQLiteDataReader> ExecuteQuery(string sql, SQLiteParameter[] parameters = null, SQLiteConnection conn = null)
        {
            bool closeConnection = false;
            try
            {
                if (conn == null)
                {
                    conn = await GetConnection();
                    closeConnection = true;
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                if (parameters != null)
                {
                    foreach(var p in parameters)
                        cmd.Parameters.Add(p);
                }

                var dataReader = cmd.ExecuteReader();
                while(await dataReader.ReadAsync())
                {
                    yield return dataReader;
                }
                await dataReader.DisposeAsync();
            }
            finally
            {
                if (closeConnection)
                    await conn?.CloseAsync();
            }
        }

        public async Task<NameValueCollection> SingleResultQuery(string sql, SQLiteParameter[] parameters = null, SQLiteConnection conn = null)
        {
            await foreach (var dr in ExecuteQuery(sql, parameters, conn))
            {
                return dr.GetValues();
            }

            return null;
        }

        public async Task<int> ExecuteNonQuery(string sql, SQLiteParameter[] parameters = null, SQLiteConnection conn = null)
        {
            bool closeConnection = false;
            try
            {
                if (conn == null)
                {
                    conn = await GetConnection();
                    closeConnection = true;
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                if (parameters != null)
                {
                    foreach(var p in parameters)
                        cmd.Parameters.Add(p);
                }
                return await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                throw;
            }
            finally
            {
                if (closeConnection)
                    await conn?.CloseAsync();
            }
        }

        public async Task<object> ExecuteScalar(string sql, SQLiteParameter[] parameters = null, SQLiteConnection conn = null)
        {
            bool closeConnection = false;
            try
            {
                if (conn == null)
                {
                    conn = await GetConnection();
                    closeConnection = true;
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                if (parameters != null)
                {
                    foreach(var p in parameters)
                        cmd.Parameters.Add(p);
                }

                return await cmd.ExecuteScalarAsync();
            }
            finally
            {
                if (closeConnection)
                    await conn?.CloseAsync();
            }
        }

        public SQLiteParameter GetParameter(string name, long value)
        {
            var p = new SQLiteParameter(name, DbType.Int64);
            p.Value = value;
            return p;
        }

        public SQLiteParameter GetParameter(string name, int value)
        {
            var p = new SQLiteParameter(name, DbType.Int32);
            p.Value = value;
            return p;
        }

        public SQLiteParameter GetParameter(string name, int? value)
        {
            var p = new SQLiteParameter(name, DbType.Int32);
            p.Value = value;
            return p;
        }

        public SQLiteParameter GetParameter(string name, double value)
        {
            var p = new SQLiteParameter(name, DbType.Double);
            p.Value = value;
            return p;
        }

        public SQLiteParameter GetParameter(string name, double? value)
        {
            var p = new SQLiteParameter(name, DbType.Double);
            p.Value = value;
            return p;
        }

        public SQLiteParameter GetParameter(string name, DateTimeOffset value)
        {
            var p = new SQLiteParameter(name, DbType.Int64);
            p.Value = value.ToUnixTimeMilliseconds();
            return p;
        }

        public SQLiteParameter GetParameter(string name, Guid value)
        {
            var p = new SQLiteParameter(name, DbType.Double);
            p.Value = value.ToByteArray();
            return p;
        }

        public SQLiteParameter GetParameter(string name, string value)
        {
            var p = new SQLiteParameter(name, DbType.String);
            p.Value = value;
            return p;
        }
    }
}
