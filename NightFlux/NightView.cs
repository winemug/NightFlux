using Newtonsoft.Json;
using NightFlux.Model;
using NightFlux.View;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TimeValue = System.Collections.Generic.KeyValuePair<System.DateTimeOffset, double?>;

namespace NightFlux
{
    public class NightView
    {
        private Configuration Configuration;

        public NightView(Configuration configuration)
        {
            Configuration = configuration;
        }

        //public async IAsyncEnumerable<TimeValue> InsulinSimulation(DateTimeOffset start, DateTimeOffset end)
        //{
        //}

        public async IAsyncEnumerable<TimeValue> GlucoseValues(DateTimeOffset start, DateTimeOffset end)
        {
            using var nsql = await NightSql.GetInstance(Configuration);
            var sql = "SELECT time, value FROM bg WHERE time >= @t1 AND time < @t2 ORDER BY time";
            var parameters = new [] { nsql.GetParameter("t1", start), nsql.GetParameter("t2", end) };
            await foreach(var dr in nsql.ExecuteQuery(sql, parameters))
            {
                yield return new TimeValue(DateTimeOffset.FromUnixTimeMilliseconds(dr.GetInt64(0)),
                    dr.GetDouble(1));
            }
        }

        public async IAsyncEnumerable<TimeValue> CarbEntries(DateTimeOffset start, DateTimeOffset end)
        {
            using var nsql = await NightSql.GetInstance(Configuration);
            yield return new TimeValue(DateTimeOffset.UnixEpoch, 0);
        }

        public async IAsyncEnumerable<TimeValue> BolusEntries(DateTimeOffset start, DateTimeOffset end)
        {
            using var nsql = await NightSql.GetInstance(Configuration);
            var sql = "SELECT time, amount FROM bolus WHERE time >= @t1 AND time < @t2 ORDER BY time";
            var parameters = new [] { nsql.GetParameter("t1", start), nsql.GetParameter("t2", end) };
            await foreach (var dr in nsql.ExecuteQuery(sql, parameters))
            {
                yield return new TimeValue(DateTimeOffset.FromUnixTimeMilliseconds(dr.GetInt64(0)), dr.GetDouble(1));
            }
        }

        public async IAsyncEnumerable<TimeValue> ExtendedBolusRates(DateTimeOffset start, DateTimeOffset end)
        {
            using var nsql = await NightSql.GetInstance(Configuration);

            var ebTimeline = new IntervalCollection<ExtendedBolus>();
            var sql = "SELECT time, amount, duration FROM extended_bolus WHERE time < @t1 ORDER BY time DESC LIMIT 1";
            await foreach(var bpr in nsql.ExecuteQuery(sql, new [] { nsql.GetParameter("t1", start)}))
            {
                var previousEb = await ReadExtendedBolus(bpr);
                AddExtendedBolusToTimeline(ebTimeline, previousEb);
            }

            sql = "SELECT time, amount, duration FROM extended_bolus WHERE time >= @t1 AND time < @t2 ORDER BY time";
            var parameters = new [] { nsql.GetParameter("t1", start), nsql.GetParameter("t2", end) };
            await foreach(var dr in nsql.ExecuteQuery(sql, parameters))
            {
                var eb = await ReadExtendedBolus(dr);
                AddExtendedBolusToTimeline(ebTimeline, eb);
            }

            yield return new TimeValue(start, 0);
        }

        public async IAsyncEnumerable<TimeValue> BasalRates(DateTimeOffset start, DateTimeOffset end)
        {
            using var nsql = await NightSql.GetInstance(Configuration);

            var basalTimeline = new IntervalCollection<BasalProfile>();
            var tempBasalTimeline = new IntervalCollection<TempBasal>();

            var bpStart = start;

            var sql = "SELECT * FROM basal WHERE duration = 0 AND time < @t1 ORDER BY time DESC LIMIT 1";
            await foreach(var bpr in nsql.ExecuteQuery(sql, new [] { nsql.GetParameter("t1", start)}))
            {
                var earlyProfile = await ReadProfile(bpr);
                AddProfileToTimeline(basalTimeline, earlyProfile);
                bpStart = earlyProfile.Time;
            }

            sql = "SELECT * FROM basal WHERE time >= @t1 AND time < @t2 ORDER BY time";
            await foreach(var bpr in nsql.ExecuteQuery(sql, new [] { nsql.GetParameter("t1", bpStart), nsql.GetParameter("t2", end)}))
            {
                var profile = await ReadProfile(bpr);
                AddProfileToTimeline(basalTimeline, profile);
            }

            sql = "SELECT * FROM tempbasal WHERE time < @t1 ORDER BY time DESC LIMIT 1";
            await foreach(var tbr in nsql.ExecuteQuery(sql, new [] { nsql.GetParameter("t1", start)}))
            {
                var earlyTempBasal = await ReadTempBasal(tbr);
                AddTempbasalToTimeline(tempBasalTimeline, earlyTempBasal);
            }

            sql = "SELECT * FROM tempbasal WHERE time >= @t1 AND time < @t2 ORDER BY time";
            await foreach(var tbr in nsql.ExecuteQuery(sql, new [] { nsql.GetParameter("t1", start), nsql.GetParameter("t2", end)}))
            {
                AddTempbasalToTimeline(tempBasalTimeline, await ReadTempBasal(tbr));
            }

            var pointOfInterest = start;
            double? lastRate = null;
            var activeBasalRates = new double[48];
            var activeUtcOffset = 0;

            while(pointOfInterest < end)
            {
                var activeBasalInterval = basalTimeline[pointOfInterest];
                var activeBasal = activeBasalInterval?.Value;
                var activeTempBasalInterval = tempBasalTimeline[pointOfInterest];
                var activeTempBasal = activeTempBasalInterval?.Value;

                if (activeBasal.HasValue)
                {
                    for (int i = 0; i < 48; i++)
                        activeBasalRates[i] = activeBasal.Value.BasalRates[i];
                    activeUtcOffset = activeBasal.Value.UtcOffsetInMinutes;
                }

                if (activeTempBasal.HasValue)
                {
                    if (activeTempBasal.Value.AbsoluteRate.HasValue)
                    {
                        for (int i = 0; i < 48; i++)
                            activeBasalRates[i] = activeTempBasal.Value.AbsoluteRate.Value;
                    }
                    else if (activeTempBasal.Value.Percentage.HasValue)
                    {
                        for (int i = 0; i < 48; i++)
                        {
                            activeBasalRates[i] *= activeTempBasal.Value.Percentage.Value;
                            activeBasalRates[i] /= 100;
                        }
                    }
                }

                var scheduleOffset = pointOfInterest.AddMinutes(activeUtcOffset);
                var scheduleIndex = scheduleOffset.Hour * 2;
                if (scheduleOffset.Minute > 30)
                    scheduleIndex++;

                var scheduledRate = activeBasalRates[scheduleIndex];

                if (!lastRate.HasValue
                    || !lastRate.Value.IsSameAs(scheduledRate, 0.05m))
                {
                    yield return new TimeValue(pointOfInterest, scheduledRate);

                    lastRate = scheduledRate;
                }

                pointOfInterest = GetNextHalfHourMark(pointOfInterest);
                
                if (activeBasalInterval?.End < pointOfInterest)
                    pointOfInterest = activeBasalInterval.End;

                if (activeTempBasalInterval?.End < pointOfInterest)
                    pointOfInterest = activeTempBasalInterval.End;
            }
        }

        public async IAsyncEnumerable<TimeValue> ExtendedBolusTicks(DateTimeOffset start, DateTimeOffset end)
        {
            var lastRate = new TimeValue();
            await foreach (var rate in ExtendedBolusRates(start, end))
            {
                if (lastRate.Value.HasValue)
                {
                    foreach (var extendedTick in TicksAtRate(lastRate.Value.Value, lastRate.Key, rate.Key))
                    {
                        yield return extendedTick;
                    }
                }
                lastRate = rate;
            }

            if (lastRate.Value.HasValue)
            {
                foreach (var basalTick in TicksAtRate(lastRate.Value.Value, lastRate.Key, end))
                {
                    yield return basalTick;
                }
            }
        }

        public async IAsyncEnumerable<TimeValue> BasalTicks(DateTimeOffset start, DateTimeOffset end)
        {
            var lastBasalRate = new TimeValue();
            await foreach (var basalRate in BasalRates(start, end))
            {
                if (lastBasalRate.Value.HasValue)
                {
                    foreach (var basalTick in TicksAtRate(lastBasalRate.Value.Value, lastBasalRate.Key, basalRate.Key))
                    {
                        yield return basalTick;
                    }
                }
                lastBasalRate = basalRate;
            }

            if (lastBasalRate.Value.HasValue)
            {
                foreach (var basalTick in TicksAtRate(lastBasalRate.Value.Value, lastBasalRate.Key, end))
                {
                    yield return basalTick;
                }
            }

        }

        public async IAsyncEnumerable<TimeValue> BolusTicks(DateTimeOffset start, DateTimeOffset end)
        {
            await foreach (var bolusEntry in BolusEntries(start, end))
            {
                foreach (var bolusTick in BolusTicks(bolusEntry.Value.Value, bolusEntry.Key))
                    yield return bolusTick;
            }
        }

        private IEnumerable<TimeValue> BolusTicks(double amount, DateTimeOffset start)
        {
            var tickCount = (int) Math.Round(amount / 0.05);
            if (tickCount > 0)
            {
                var tickInterval = TimeSpan.FromSeconds(2);
                var tickAt = start;
                while (tickCount > 0)
                {
                    yield return new TimeValue(tickAt, 0.05);
                    tickCount--;
                    tickAt = tickAt.Add(tickInterval);
                }
            }
        }

        private IEnumerable<TimeValue> TicksAtRate(double rate, DateTimeOffset start, DateTimeOffset end)
        {
            var tickCount = (int) Math.Round(rate / 0.05);
            if (tickCount > 0)
            {
                var tickInterval = TimeSpan.FromMilliseconds(TimeSpan.FromHours(1).TotalMilliseconds / tickCount);
                var tickTick = start.Add(tickInterval);

                while (tickTick < end)
                {
                    yield return new TimeValue(tickTick, 0.05);
                    tickTick = tickTick.Add(tickInterval);
                }
            }
        }

        private DateTimeOffset GetNextHalfHourMark(DateTimeOffset dt)
        {
            dt = dt.AddMinutes(30);
            if (dt.Minute < 30)
            {
                return new DateTimeOffset(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, dt.Offset);
            }
            else
            {
                return new DateTimeOffset(dt.Year, dt.Month, dt.Day, dt.Hour, 30, 0, dt.Offset);
            }
        }

        private void AddProfileToTimeline(IntervalCollection<BasalProfile> timeline, BasalProfile profile)
        {
            if (profile.Duration == 0)
            {
                timeline.Add(profile.Time, null, profile);
            }
            else
            {
                timeline.Add(profile.Time, profile.Time.AddMinutes(profile.Duration), profile);
            }
        }

        private void AddTempbasalToTimeline(IntervalCollection<TempBasal> timeline, TempBasal tempBasal)
        {
            if (tempBasal.Duration == 0)
            {
                timeline.Crop(tempBasal.Time);
            }
            else
            {
                timeline.Add(tempBasal.Time, tempBasal.Time.AddMinutes(tempBasal.Duration), tempBasal);
            }
        }

        private void AddExtendedBolusToTimeline(IntervalCollection<ExtendedBolus> timeline, ExtendedBolus extendedBolus)
        {
            if (extendedBolus.Duration == 0)
            {
                timeline.Crop(extendedBolus.Time);
            }
            else
            {
                timeline.Add(extendedBolus.Time, extendedBolus.Time.AddMinutes(extendedBolus.Duration), extendedBolus);
            }
        }

        private async Task<BasalProfile> ReadProfile(SQLiteDataReader dr)
        {
            return new BasalProfile
            {
                Time = DateTimeOffset.FromUnixTimeMilliseconds(dr.GetInt64(0)),
                UtcOffsetInMinutes = dr.GetInt32(1),
                Duration = dr.GetInt32(2),
                BasalRates = JsonConvert.DeserializeObject<double[]>(dr.GetString(3))
            };
        }

        private async Task<TempBasal> ReadTempBasal(SQLiteDataReader dr)
        {
            return new TempBasal
            {
                Time = DateTimeOffset.FromUnixTimeMilliseconds(dr.GetInt64(0)),
                Duration = dr.GetInt32(1),
                AbsoluteRate = dr.IsDBNull(2) ? null : (double?)dr.GetDouble(2),
                Percentage = dr.IsDBNull(3) ? null: (int?)dr.GetInt32(3)
            };
        }

        private async Task<ExtendedBolus> ReadExtendedBolus(SQLiteDataReader dr)
        {
            return new ExtendedBolus
            {
                Time = DateTimeOffset.FromUnixTimeMilliseconds(dr.GetInt64(0)),
                Amount = dr.IsDBNull(1) ? null : (double?)dr.GetDouble(1),
                Duration = dr.GetInt32(2)
            };
        }
    }
}
