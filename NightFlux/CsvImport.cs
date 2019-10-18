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
        private StreamWriter BpStream;
        private FileStream BolusStream;
        private FileStream BasalStream;

        public CsvImport(IConfigurationSection cs)
        {
            var bgPath = cs["bg_path"] ?? "nf_bg.csv";
            var bpPath = cs["basal_profile_path"] ?? "basal_profile.csv";
            var bolusPath = cs["bolus_path"] ?? "nf_bolus.csv";
            var basalPath = cs["basal_path"] ?? "nf_basal.csv";

            BgStream = new StreamWriter(bgPath, true);
            BpStream = new StreamWriter(bpPath, true);
        }

        public void Dispose()
        {
            BgStream?.Dispose();
            BpStream?.Dispose();
            BolusStream?.Dispose();
            BasalStream?.Dispose();
        }

        public async Task ImportBg(BgValue bgv)
        {
            await BgStream.WriteLineAsync($"{bgv.Time.ToUnixTimeMilliseconds()},{bgv.Value.ToString(CultureInfo.InvariantCulture)}");
        }
        public async Task ImportProfile(BasalProfile bp)
        {
            await BpStream.WriteAsync($"{bp.Time.ToUnixTimeMilliseconds()},{bp.UtcOffsetInMinutes.ToString(CultureInfo.InvariantCulture)}");
            foreach(var rate in bp.BasalRates)
            {
                await BpStream.WriteAsync($",{rate.ToString(CultureInfo.InvariantCulture)}");
            }
            await BpStream.WriteAsync(Environment.NewLine);
        }
    }
}
