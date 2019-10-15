using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NightFlux
{
    public class CsvReader : IDisposable
    {
        private StreamReader BgStream;

        public CsvReader(IConfigurationSection cs)
        {
            var bgPath = cs["bg_path"];
            var bolusPath = cs["bolus_path"];
            var basalPath = cs["basal_path"];

            if (File.Exists(bgPath))
                BgStream = new StreamReader(bgPath);
        }

        public async Task<long> GetLastBgTimestamp()
        {
            string lastLine = null;
            if (BgStream != null)
            {
                while (!BgStream.EndOfStream)
                {
                    var line = await BgStream.ReadLineAsync();
                    if (line.Length > 0)
                    {
                        lastLine = line;
                    }
                }
            }

            long ts = 0;
            if (lastLine != null)
            {
                var cols = lastLine.Split(',');
                if (cols.Length == 2)
                    ts = long.Parse(cols[0]);
            }

            return ts;
        }

        public async Task<Dictionary<DateTimeOffset, decimal>> GetBgValues(DateTimeOffset? startIncluding = null, DateTimeOffset? endExcluding = null)
        {
            var dict = new Dictionary<DateTimeOffset, decimal>();
            if (BgStream != null)
            {
                while (!BgStream.EndOfStream)
                {
                    var line = await BgStream.ReadLineAsync();
                    if (line.Length > 0)
                    {
                        var cols = line.Split(',');
                        long timestamp = long.Parse(cols[0], CultureInfo.InvariantCulture);
                        decimal bgv = decimal.Parse(cols[1], CultureInfo.InvariantCulture);
                        var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

                        if (startIncluding.HasValue && dt < startIncluding)
                            continue;

                        if (endExcluding.HasValue && dt >= endExcluding)
                            continue;

                        dict[dt] = bgv;
                    }
                }
            }
            return dict;
        }

        public void Dispose()
        {
            BgStream?.Dispose();
        }
    }
}
