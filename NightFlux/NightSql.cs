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
        private ConcurrentBag<Task> BatchTasks;

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
            BatchTasks = new ConcurrentBag<Task>();
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

                if (BatchEntities.Count > 1024)
                {
                    var list = new List<IEntity>();
                    IEntity iv;
                    while(BatchEntities.TryDequeue(out iv))
                        list.Add(iv);

                    BatchTasks.Add(InsertBatchEntities(list));
                }
            }
        }

        public async Task FinalizeBatchImport()
        {
            var list = new List<IEntity>();
            IEntity iv;
            while(BatchEntities.TryDequeue(out iv))
                list.Add(iv);
            BatchTasks.Add(InsertBatchEntities(list));
            await Task.WhenAll(BatchTasks.ToArray());
        }

        public async Task RemoveDuplicateCarbs()
        {
            using var conn = await GetConnection();
            var duplicateIds = new List<long>();
            await foreach (var dr in ExecuteQuery("SELECT import_id, COUNT(*) FROM carb GROUP BY import_id HAVING COUNT(*) > 1", null, conn))
            {
                var importId = dr.GetString(0);
                var duplicateCount = dr.GetInt64(1) - 1;
                await foreach (var rowInfo in ExecuteQuery("SELECT rowid FROM carb WHERE import_id = @i ORDER BY time DESC LIMIT @k",
                    new [] { GetParameter("i", importId), GetParameter("k", duplicateCount) }, conn))
                {
                    duplicateIds.Add(rowInfo.GetInt64(0));
                }
            }

            using var tran = await conn.BeginTransactionAsync();
            foreach(var duplicateId in duplicateIds)
            {
                await ExecuteNonQuery("DELETE FROM carb WHERE rowid = @d",
                    new [] { GetParameter("d", duplicateId) }, conn);
            }
            await tran.CommitAsync();
        }

        private async Task InsertBatchEntities(List<IEntity> entities)
        {
            using var conn = await GetConnection();
            using var tran = await conn.BeginTransactionAsync();
            foreach(var iv in entities)
            {
                await InsertEntity(iv, conn);
            }
            await tran.CommitAsync();
        }

        private async Task InsertEntity(IEntity iv, SQLiteConnection conn = null)
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

        private async Task ImportBG(BgValue bgValue, SQLiteConnection conn)
        {
            await ExecuteNonQuery("INSERT INTO bg(time,value) VALUES(@t, @v)",
                new []
                {
                    GetParameter("t", bgValue.Time),
                    GetParameter("v", bgValue.Value)
                }, conn);
        }

        private async Task ImportProfile(BasalProfile basalProfile, SQLiteConnection conn)
        {
            await ExecuteNonQuery("INSERT INTO basal(time,utc_offset,duration,rates) VALUES(@t, @u, @d, @r)",
                new []
                {
                    GetParameter("t", basalProfile.Time),
                    GetParameter("u", basalProfile.UtcOffsetInMinutes),
                    GetParameter("d", basalProfile.Duration),
                    GetParameter("r", JsonConvert.SerializeObject(basalProfile.BasalRates))
                }, conn);
        }

        private async Task ImportTempBasal(TempBasal tempBasal, SQLiteConnection conn)
        {
            await ExecuteNonQuery("INSERT INTO tempbasal(time,duration,absolute,percentage) VALUES(@t, @d, @u, @p)",
                new []
                {
                    GetParameter("t", tempBasal.Time),
                    GetParameter("d", tempBasal.Duration),
                    GetParameter("u", tempBasal.AbsoluteRate),
                    GetParameter("p", tempBasal.Percentage)
                }, conn);
        }

        private async Task ImportBolus(Bolus bolus, SQLiteConnection conn)
        {
            await ExecuteNonQuery("INSERT INTO bolus(time,amount) VALUES(@t, @a)",
                new []
                {
                    GetParameter("t", bolus.Time),
                    GetParameter("a", bolus.Amount)
                }, conn);
        }

        private async Task ImportExtendedBolus(ExtendedBolus extendedBolus, SQLiteConnection conn)
        {
            await ExecuteNonQuery("INSERT INTO extended_bolus(time,amount,duration) VALUES(@t, @a, @d)",
                new []
                {
                    GetParameter("t", extendedBolus.Time),
                    GetParameter("a", extendedBolus.Amount),
                    GetParameter("d", extendedBolus.Duration)
                }, conn);
        }

        private async Task ImportCarb(Carb carb, SQLiteConnection conn)
        {
            await ExecuteNonQuery("INSERT INTO carb(time,amount,import_id) VALUES(@t, @a, @i)",
                new []
                {
                    GetParameter("t", carb.Time),
                    GetParameter("a", carb.Amount),
                    GetParameter("i", carb.ImportId),
                }, conn);
        }

        public async Task<long> GetLastBgDate()
        {
            await foreach (var dr in ExecuteQuery("SELECT time FROM bg ORDER BY time DESC LIMIT 1"))
            {
                return dr.GetInt64(0);
            }
            return 0;
        }

        public async Task<long> GetLastProfileChangeDate()
        {
            await foreach (var dr in ExecuteQuery("SELECT time FROM basal ORDER BY time DESC LIMIT 1"))
            {
                return dr.GetInt64(0);
            }
            return 0;
        }

        public async Task<long> GetLastTempBasalDate()
        {
            await foreach (var dr in ExecuteQuery("SELECT time FROM tempbasal ORDER BY time DESC LIMIT 1"))
            {
                return dr.GetInt64(0);
            }
            return 0;
        }

        public async Task<long> GetLastBolusDate()
        {
            await foreach (var dr in ExecuteQuery("SELECT time FROM bolus ORDER BY time DESC LIMIT 1"))
            {
                return dr.GetInt64(0);
            }
            return 0;
        }

        public async Task<long> GetLastExtendedBolusDate()
        {
            await foreach (var dr in ExecuteQuery("SELECT time FROM extended_bolus ORDER BY time DESC LIMIT 1"))
            {
                return dr.GetInt64(0);
            }
            return 0;
        }

        public async Task<long> GetLastCarbDate()
        {
            await foreach (var dr in ExecuteQuery("SELECT time FROM carb ORDER BY time DESC LIMIT 1"))
            {
                return dr.GetInt64(0);
            }
            return 0;
        }

        private async Task Initialize()
        {
            await ExecuteNonQuery("CREATE TABLE IF NOT EXISTS bg" +
                "(time INTEGER, value REAL);");

            await ExecuteNonQuery("CREATE TABLE IF NOT EXISTS basal" +
                "(time INTEGER, utc_offset INTEGER, duration INTEGER, rates TEXT);");
            
            await ExecuteNonQuery("CREATE TABLE IF NOT EXISTS tempbasal" +
                "(time INTEGER, duration INTEGER, absolute REAL, percentage INTEGER);");

            await ExecuteNonQuery("CREATE TABLE IF NOT EXISTS bolus" +
                "(time INTEGER, amount REAL);");

            await ExecuteNonQuery("CREATE TABLE IF NOT EXISTS extended_bolus" +
                "(time INTEGER, amount REAL, duration INTEGER);");

            await ExecuteNonQuery("CREATE TABLE IF NOT EXISTS carb" +
                "(time INTEGER, amount REAL, import_id TEXT);");
        }

        private async Task<SQLiteConnection> GetConnection()
        {
            var conn = new SQLiteConnection(SqliteConnectionString);
            await conn.OpenAsync();
            return conn;
        }

        private async IAsyncEnumerable<SQLiteDataReader> ExecuteQuery(string sql, SQLiteParameter[] parameters = null, SQLiteConnection conn = null)
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

        private async Task<int> ExecuteNonQuery(string sql, SQLiteParameter[] parameters = null, SQLiteConnection conn = null)
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

        private async Task<object> ExecuteScalar(string sql, SQLiteParameter[] parameters = null, SQLiteConnection conn = null)
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

        private SQLiteParameter GetParameter(string name, long value)
        {
            var p = new SQLiteParameter(name, DbType.Int64);
            p.Value = value;
            return p;
        }

        private SQLiteParameter GetParameter(string name, int value)
        {
            var p = new SQLiteParameter(name, DbType.Int32);
            p.Value = value;
            return p;
        }

        private SQLiteParameter GetParameter(string name, int? value)
        {
            var p = new SQLiteParameter(name, DbType.Int32);
            p.Value = value;
            return p;
        }

        private SQLiteParameter GetParameter(string name, decimal value)
        {
            var p = new SQLiteParameter(name, DbType.Decimal);
            p.Value = value;
            return p;
        }

        private SQLiteParameter GetParameter(string name, decimal? value)
        {
            var p = new SQLiteParameter(name, DbType.Decimal);
            p.Value = value;
            return p;
        }

        private SQLiteParameter GetParameter(string name, DateTimeOffset value)
        {
            var p = new SQLiteParameter(name, DbType.Int64);
            p.Value = value.ToUnixTimeMilliseconds();
            return p;
        }

        private SQLiteParameter GetParameter(string name, Guid value)
        {
            var p = new SQLiteParameter(name, DbType.Decimal);
            p.Value = value.ToByteArray();
            return p;
        }

        private SQLiteParameter GetParameter(string name, string value)
        {
            var p = new SQLiteParameter(name, DbType.String);
            p.Value = value;
            return p;
        }
    }
}
