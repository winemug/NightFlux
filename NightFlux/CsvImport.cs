using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NightFlux
{
    public class CsvImport : IDisposable
    {
        private StreamWriter BgStream;
        private FileStream BolusStream;
        private FileStream BasalStream;

        public CsvImport(IConfigurationSection cs)
        {
            var bgPath = cs["bg_path"];
            var bolusPath = cs["bolus_path"];
            var basalPath = cs["basal_path"];

            BgStream = new StreamWriter(bgPath);
        }

        public async Task<long> GetLastBgTimestamp()
        {
            return 0;
        }

        public void Dispose()
        {
            BgStream?.Close();
            BolusStream?.Close();
            BasalStream?.Close();
        }

        public async Task ImportBg(BgValue bgv)
        {
            await BgStream.WriteLineAsync($"{bgv.Time.ToUnixTimeMilliseconds()},{bgv.Value.ToString(CultureInfo.InvariantCulture)}");
        }
    }
}
