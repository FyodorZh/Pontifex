namespace Shared.MetaGame
{
    public struct Range<T>
    {
        public Range(T min, T max)
            : this()
        {
            Min = min;
            Max = max;
        }

        public T Min { get; private set; }

        public T Max { get; private set; }
    }
}
