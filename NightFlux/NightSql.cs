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

        private ConcurrentQueue<BgValue> BatchBgValues;
        private ConcurrentBag<Task> BatchTasks;

        private NightSql(Configuration configuration)
        {
            SqliteConnectionString = $"Data Source={configuration.SqlitePath};Version=3;Pooling=True;PRAGMA journal_mode=WAL";
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

        public async Task StartBatchImportBg()
        {
            BatchBgValues = new ConcurrentQueue<BgValue>();
            BatchTasks = new ConcurrentBag<Task>();
        }

        public async Task ImportBg(BgValue bgValue)
        {
            BatchBgValues.Enqueue(bgValue);

            if (BatchBgValues.Count > 1024)
            {
                var list = new List<BgValue>();
                BgValue bgv;
                while(BatchBgValues.TryDequeue(out bgv))
                    list.Add(bgValue);

                BatchTasks.Add(InsertBatchBg(list));
            }
        }

        public async Task FinalizeBatchImportBg()
        {
            var list = new List<BgValue>();
            BgValue bgValue;
            while(BatchBgValues.TryDequeue(out bgValue))
                list.Add(bgValue);
            BatchTasks.Add(InsertBatchBg(list));
            await Task.WhenAll(BatchTasks.ToArray());
        }

        private async Task InsertBatchBg(List<BgValue> bgValues)
        {
            using var conn = await GetConnection();
            using var tran = await conn.BeginTransactionAsync();
            foreach(var bgValue in bgValues)
            {
                await ExecuteNonQuery("INSERT INTO bg(time,value) VALUES(@t, @v)",
                    new []
                    {
                        GetParameter("t", bgValue.Time),
                        GetParameter("v", bgValue.Value)
                    }, conn);
            }
            await tran.CommitAsync();
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
                "(time INTEGER, value REAL);");

            await ExecuteNonQuery("CREATE TABLE IF NOT EXISTS basal" +
                "(time INTEGER, value REAL);");
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
            finally
            {
                if (closeConnection)
                    await conn?.CloseAsync();
            }
        }

        private SQLiteParameter GetParameter(string name, decimal value)
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
