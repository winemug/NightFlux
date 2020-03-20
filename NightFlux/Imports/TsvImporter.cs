using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using NightFlux.Data;
using NightFlux.Model;

namespace NightFlux.Imports
{
    public static class TsvImporter
    {
        public static async Task Run(Configuration configuration)
        {

            var folder = configuration.OmniCoreExportsFolder;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return;

            var nsql = await NightSql.GetInstance(configuration);

            foreach (var filePath in Directory.GetFiles(folder))
            {
                var ps = await ReadPodSession(filePath);
                await nsql.ImportOmniCorePodSession(ps);
            }
        }
        private static async Task<PodSession> ReadPodSession(string path)
        {
            using var sr = new StreamReader(path);

            var year = 1;
            var month = 1;
            var day = 1;
            var utcOffset = 60;
            string hormone = "insulin";
            decimal solution = 100;
            decimal baseRate = 0;
            decimal currentRate = 0;
            decimal? tbRate = null;
            int defaultTbDuration = 30;
            DateTimeOffset tbEnd = new DateTimeOffset();

            PodSession ps = null;

            string site = "";

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
                        year = int.Parse(line[1..5]);
                        month = int.Parse(line[5..7]);
                        day = int.Parse(line[7..9]);
                        //if (line.Length > 12)
                        //    utcOffset = int.Parse(line[12..]);
                        continue;
                    }

                    var cols = line.Split('\t');

                    var hour = int.Parse(cols[0][0..2]);
                    var minute = int.Parse(cols[0][2..4]);
                    var second = 0;
                    if (cols[0].Length > 4)
                        second = int.Parse(cols[0][4..5]);

                    var entryDate = new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.FromMinutes(utcOffset));
                    var entryType = cols[1];

                    if (tbRate.HasValue && tbEnd > entryDate)
                    {
                        currentRate = baseRate;
                        tbRate = null;
                        ps.BasalRate(entryDate, (int) (currentRate / 0.05m));
                    }

                    switch (entryType)
                    {
                        case "s":
                            hormone = cols[2];
                            solution = decimal.Parse(cols[3], CultureInfo.InvariantCulture);
                            baseRate = decimal.Parse(cols[4], CultureInfo.InvariantCulture);
                            site = $"{entryDate:yyMMdd-hhmm}-{hormone}-{solution}";
                            currentRate = baseRate;

                            ps = new PodSession
                            {
                                Hormone = hormone == "insulin" ? HormoneType.InsulinAspart : HormoneType.Glucagon,
                                UnitsPerMilliliter = solution,
                                Name = site,
                                Activated = entryDate
                            };

                            ps.BasalRate(entryDate, (int) (currentRate / 0.05m));


                            break;
                        case "x":
                            currentRate = decimal.Parse(cols[2], CultureInfo.InvariantCulture);
                            ps.BasalRate(entryDate, (int) (currentRate / 0.05m));
                            break;
                        case "z":
                        case "b":
                            var bolusAmount = decimal.Parse(cols[2], CultureInfo.InvariantCulture);
                            if (cols.Length > 3 && cols[3].Trim().Length > 0)
                            {
                                var duration = int.Parse(cols[3]);
                                ps.ExtendedBolus(entryDate, (int) (bolusAmount / 0.05m), duration);
                            }
                            else
                            {
                                ps.Bolus(entryDate, (int) (bolusAmount / 0.05m));
                            }

                            break;
                        case "tb":
                            tbRate = decimal.Parse(cols[2], CultureInfo.InvariantCulture);
                            var tbDuration = defaultTbDuration;
                            if (cols.Length > 3 && cols[3].Trim().Length > 0)
                            {
                                tbDuration = int.Parse(cols[3]);
                            }

                            currentRate = tbRate.Value;
                            ps.BasalRate(entryDate, (int) (currentRate / 0.05m));
                            tbEnd = entryDate.AddMinutes(tbDuration);
                            break;
                        case "tbc":
                            currentRate = baseRate;
                            tbRate = null;
                            ps.BasalRate(entryDate, (int) (currentRate / 0.05m));
                            break;
                        case "e":
                            ps.BasalRate(entryDate, (int) 0);
                            ps.Deactivated = entryDate;
                            break;
                    }
                }
            }
            catch
            {
                Debug.Write($"Offending line: {line}");
                throw;
            }

            return ps;
        }

    }
}
