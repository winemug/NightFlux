using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NightFlux
{
    public class NightSync : IDisposable
    {
        private string DatabasePath;
        public NightSync(Configuration configuration)
        {
            DatabasePath = configuration.SqlitePath;
        }

        public void Dispose()
        {
        }

        public async Task ImportBg()
        {
        }

        public async Task ImportBasals()
        {
        }

        public async Task ImportBoluses()
        {
        }

        public async Task ImportCarbs()
        {
        }
    }
}
