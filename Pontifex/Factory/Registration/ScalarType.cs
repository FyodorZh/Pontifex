namespace Pontifex.Factory.Registration
{
    /// <summary>
    /// The inclusive range of an integer parameter, field, or array element.
    /// Constructors accept unsorted bounds; the formula registry owns range
    /// validation so that <c>E_REG_INVALID_INTEGER_RANGE</c> is reachable through
    /// the core registration adapter.
    /// </summary>
    public sealed class IntegerType
    {
        public IntegerType(long min, long max)
        {
            Min = min;
            Max = max;
        }

        public long Min { get; }

        public long Max { get; }
    }

    /// <summary>
    /// The inclusive range of a decimal parameter, field, or array element.
    /// Constructors accept unsorted bounds; the formula registry owns range
    /// validation so that <c>E_REG_INVALID_DECIMAL_RANGE</c> is reachable through
    /// the core registration adapter.
    /// </summary>
    public sealed class DecimalType
    {
        public DecimalType(decimal min, decimal max)
        {
            Min = min;
            Max = max;
        }

        public decimal Min { get; }

        public decimal Max { get; }
    }
}
