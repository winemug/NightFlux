using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace NightFlux.View
{
    public class IntervalCollection<T> where T : struct
    {
        private List<Interval<T>> Intervals;

        public Interval<T> this[DateTimeOffset atTimeOffset]
        {
            get
            {
                var i = IndexOf(atTimeOffset);
                if (i >= 0)
                    return Intervals[i];
                return null;
            }
        }

        public IntervalCollection()
        {
            Intervals = new List<Interval<T>>();
        }

        public void Crop(DateTimeOffset when)
        {
            var i = IndexOf(when);
            if (i < 0)
                return;
            var match = Intervals[i];
            if (match.End > when)
            {
                match.End = when;
            }
        }

        private int IndexOf(DateTimeOffset when)
        {
            int i = 0;
            while (i < Intervals.Count)
            {
                var entry = Intervals[i];
                if (entry.Start <= when && entry.End > when)
                    return i;
                i++;
            }
            return -1;
        }

        public void Add(DateTimeOffset? start, DateTimeOffset? end, T value)
        {
            var newInterval = new Interval<T>
            {
                Start = start ?? DateTimeOffset.MinValue,
                End = end ?? DateTimeOffset.MaxValue,
                Value = value
            };

            if (newInterval.Start == newInterval.End)
                return;

            int i = 0;
            while (i < Intervals.Count)
            {
                var entry = Intervals[i];
                if (entry.Start > newInterval.Start)
                    break;
                i++;
            }

            if (i > 0)
            {
                var intervalBefore = Intervals[i - 1];

                if (intervalBefore.Start == newInterval.Start && intervalBefore.End == newInterval.End)
                {
                    intervalBefore.Value = newInterval.Value;
                    return;
                }
                
                if (intervalBefore.End > newInterval.Start)
                {
                    if (intervalBefore.End <= newInterval.End)
                    {
                        intervalBefore.End = newInterval.Start;
                    }
                    else
                    {
                        var cutoutFromBefore = new Interval<T> { Start = newInterval.End, End = intervalBefore.End, Value = intervalBefore.Value };
                        intervalBefore.End = newInterval.Start;
                        Intervals.Insert(i, cutoutFromBefore);
                        i++;
                    }
                }
            }

            Intervals.Insert(i, newInterval);
            i++;

            while (i < Intervals.Count)
            {
                var nextInterval = Intervals[i];
                if (nextInterval.Start >= newInterval.End)
                {
                    break;
                }
                if (nextInterval.End <= newInterval.End)
                {
                    Intervals.RemoveAt(i);
                }
                else
                {
                    nextInterval.Start = newInterval.End;
                    break;
                }
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var i in Intervals)
                sb.Append(i).AppendLine();
            return sb.ToString();
        }
    }
}
