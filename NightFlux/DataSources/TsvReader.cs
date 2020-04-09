using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using MongoDB.Driver;
using NightFlux.Data;
using NightFlux.Model;

namespace NightFlux.Imports
{
    public class TsvReader
    {
        private readonly Configuration Configuration;
        public TsvReader(Configuration configuration)
        {
            Configuration = configuration;
        }

        public async IAsyncEnumerable<PodSession> PodSessions(DateTimeOffset Start, DateTimeOffset End)
        {
            var folder = Configuration.OmniCoreExportsFolder;
            if (Directory.Exists(folder))
            {
                foreach (var filePath in Directory.GetFiles(folder))
                {
                    var ps = await ReadPodSession(filePath);
                    if (ps.Activated < End && ps.Deactivated > Start)
                        yield return ps;
                }
            }
        }
        
        private async Task<PodSession> ReadPodSession(string path)
        {
            using var sr = new StreamReader(path);
            var pds = new List<PointData>();
            
            var year = 1;
            var month = 1;
            var day = 1;
            var utcOffset = 120;

            PodSession ps = null;

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

                    switch (entryType)
                    {
                        case "s":
                            ps = new PodSession
                            {
                                Name = $"{entryDate:yyMMdd-hhmm}",
                                Activated = entryDate,
                                Hormone = cols[2] == "insulin" ? HormoneType.InsulinAspart : HormoneType.Glucagon,
                                Dilution = decimal.Parse(cols[3], CultureInfo.InvariantCulture) / 100m,
                                Composition = string.Join(' ', cols[4..]),
                            };
                            break;
                        case "x":
                            ps.SetBasalRate(entryDate, decimal.Parse(cols[2], CultureInfo.InvariantCulture));
                            break;
                        case "z":
                        case "b":
                            var bolusAmount = decimal.Parse(cols[2], CultureInfo.InvariantCulture);
                            if (cols.Length > 3 && cols[3].Trim().Length > 0)
                            {
                                ps.ExtendedBolus(entryDate,
                                    decimal.Parse(cols[2], CultureInfo.InvariantCulture),
                                    int.Parse(cols[3]));
                            }
                            else
                            {
                                ps.Bolus(entryDate, decimal.Parse(cols[2], CultureInfo.InvariantCulture));
                            }
                            break;
                        case "e":
                            ps.SetBasalRate(entryDate, 0);
                            ps.Deactivated = entryDate;
                            break;
                        default:
                            throw new ArgumentException($"unknown entry type {entryType}");
                    }
                }
            }
            catch(Exception e)
            {
                Debug.Write($"Error: {e} Offending line: {line}");
                throw;
            }
            return ps;
        }

        // pds.Add(PointData.Measurement("infusion")
        // .Tag("unit", "U/h")
        //     .Field("rate", currentRate * dilution)
        // .Field("channel", 0)
        //     .Tag("delivery", "basal")
        //     .Tag("delivery_subtype", "scheduled")
        //     .Tag("hormone", hormone)
        // .Tag("dilution", dilution.ToString(CultureInfo.InvariantCulture))
        // .Tag("composition", meddesc)
        // .Tag("session", session)
        // .Timestamp(entryDate, WritePrecision.Ms));
        
        
        // private static void AddTicks(DateTimeOffset rateStart, DateTimeOffset rateEnd, decimal rate,
        //     string hormone, string meddesc, string session,
        //     decimal dilution, bool basal,  List<PointData> pds)
        // {
        //     if (rate == 0)
        //         return;
        //     
        //     var tickIntervalMs = TimeSpan.FromHours(1).TotalMilliseconds / (double) (rate / 0.05m);
        //
        //     if (!basal)
        //         rateStart =  rateStart.AddSeconds(3);
        //     
        //     while (rateStart < rateEnd)
        //     {
        //         pds.Add(PointData.Measurement("infusion")
        //             .Tag("unit", "U")
        //             .Field("deposit", 0.05m * dilution)
        //             .Tag("hormone", hormone)
        //             .Tag("dilution", dilution.ToString(CultureInfo.InvariantCulture))
        //             .Tag("composition", meddesc)
        //             .Tag("session", session)
        //             .Timestamp(rateStart, WritePrecision.Ms));
        //         rateStart = rateStart.AddMilliseconds(tickIntervalMs);
        //     }
        // }
    }
}
