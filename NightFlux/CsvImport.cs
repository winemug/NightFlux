using NightFlux.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NightFlux
{
    public class CsvImport
    {
        private Configuration Configuration;
        public CsvImport(Configuration configuration)
        {
            Configuration = configuration;
        }

        public async Task ImportFile(string path)
        {
            using var sr = new StreamReader(path);
            using var nsql = await NightSql.GetInstance(Configuration);

            DateTimeOffset? dateStart = null;
            long msDifference = 0;

            await nsql.StartBatchImport();

            string line = null;
            try
            {
                while (!sr.EndOfStream)
                {
                    line = await sr.ReadLineAsync();
                    if (string.IsNullOrEmpty(line?.Trim()))
                        continue;

                    line = line.Replace(' ', '\t');

                    if (line.StartsWith('#'))
                    {
                        var year = int.Parse(line[1..5]);
                        var month = int.Parse(line[6..8]);
                        var day = int.Parse(line[9..11]);
                        var utcOffset = int.Parse(line[12..], CultureInfo.InvariantCulture);

                        dateStart = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.FromMinutes(utcOffset));
                        msDifference = 0;
                        continue;
                    }
                    else if (!dateStart.HasValue)
                    {
                        continue;
                    }

                    var cols = line.Split('\t');

                    var hour = int.Parse(cols[0][0..2]);
                    var minute = int.Parse(cols[0][2..4]);
                    msDifference += 340;

                    var entryDate = dateStart.Value.AddHours(hour).AddMinutes(minute).AddMilliseconds(msDifference);
                    var entryType = cols[1];

                    switch(entryType)
                    {
                        case "b":
                            await nsql.Import(new Bolus { Time = entryDate, Amount = double.Parse(cols[2], CultureInfo.InvariantCulture) });
                            break;
                        case "eb":
                            await nsql.Import(new ExtendedBolus { Time = entryDate, Amount = double.Parse(cols[2], CultureInfo.InvariantCulture), Duration = int.Parse(cols[3]) });
                            break;
                        case "ebc":
                            await nsql.Import(new ExtendedBolus { Time = entryDate, Amount = null, Duration = 0 });
                            break;
                        case "tb":
                            await nsql.Import(new TempBasal { Time = entryDate, AbsoluteRate = double.Parse(cols[2], CultureInfo.InvariantCulture), Duration = int.Parse(cols[3]) });
                            break;
                        case "tbc":
                            await nsql.Import(new TempBasal { Time = entryDate, AbsoluteRate = null, Duration = 0 });
                            break;
                        case "c":
                            await nsql.Import(new Carb { Time = entryDate, Amount = int.Parse(cols[2]), ImportId = entryDate.ToUnixTimeMilliseconds().ToString() });
                            break;
                    }
                }
            }
            catch
            {
                Debug.Write($"Offending line: {line}");
                throw;
            }
            await nsql.FinalizeBatchImport();
        }
    }
}
