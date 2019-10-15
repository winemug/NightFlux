using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace NightFlux
{
    public class SqliteImport : IDisposable
    {
        private string DatabasePath;
        public SqliteImport(IConfigurationSection cs)
        {
            DatabasePath = cs["db_file"] ?? "nf.sqlite";
        }

        public void Dispose()
        {
        }
    }
}
