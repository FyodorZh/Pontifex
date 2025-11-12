using System.Collections.Generic;

namespace Shared
{
    public class IntMinHistory : ExtremumHistory<int>
    {
        public IntMinHistory(System.TimeSpan period)
            : base(period, UtcNowDateTimeProvider.Instance)
        {
        }

        public IntMinHistory(System.TimeSpan period, IDateTimeProvider dateTimeProvider)
            : base(period, dateTimeProvider)
        {
        }

        protected override void SetToMinValue(out int value)
        {
            value = int.MaxValue;
        }

        protected override int Compare(int d1, int d2)
        {
            return d2.CompareTo(d1);
        }

        protected override void GetExtremum(Queue<int>.Enumerator enumerator, out int extremum, out int count)
        {
            count = 0;
            extremum = int.MaxValue;
            using (enumerator)
            {
                while (enumerator.MoveNext())
                {
                    int element = enumerator.Current;
                    if (element < extremum)
                    {
                        extremum = element;
                        count = 1;
                    }
                    else if (element == extremum)
                    {
                        count += 1;
                    }
                }
            }
        }
    }
}