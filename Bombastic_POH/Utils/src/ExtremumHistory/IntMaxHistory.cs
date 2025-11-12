namespace Shared
{
    public class IntMaxHistory : ExtremumHistory<int>
    {
        public IntMaxHistory(System.TimeSpan period)
            : base(period, UtcNowDateTimeProvider.Instance)
        {
        }
        public IntMaxHistory(System.TimeSpan period, IDateTimeProvider dateTimeProvide)
            : base(period, dateTimeProvide)
        {
        }

        protected override void SetToMinValue(out int value)
        {
            value = int.MinValue;
        }

        protected override int Compare(int d1, int d2)
        {
            return d1.CompareTo(d2);
        }
    }
}