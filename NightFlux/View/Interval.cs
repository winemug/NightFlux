using System;

namespace NightFlux.View
{
    public class Interval<T>
    {
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }

        public T Value;

        public override string ToString()
        {
            return $"Start: {Start}\t\tEnd: {End}\t\tValue: {Value}";
        }
    }
}
