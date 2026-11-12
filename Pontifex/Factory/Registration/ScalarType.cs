namespace Pontifex.Factory.Registration
{
    /// <summary>
    /// The closed hierarchy of scalar element types allowed in an array field.
    /// The constructor is internal so transport packages can declare array fields
    /// using <see cref="StringType"/>, <see cref="IntegerType"/>, or
    /// <see cref="DecimalType"/> but cannot add element kinds.
    /// </summary>
    public abstract class SingleType
    {
        internal SingleType()
        {
        }
    }

    /// <summary>
    /// A string array element with an optional per-element validator.
    /// </summary>
    public sealed class StringType : SingleType
    {
        public StringType(ScalarValidator? validate = null)
        {
            Validate = validate;
        }

        public ScalarValidator? Validate { get; }
    }

    /// <summary>
    /// The inclusive range of an integer parameter, field, or array element.
    /// Constructors accept unsorted bounds; the formula registry owns range
    /// validation so that <c>E_REG_INVALID_INTEGER_RANGE</c> is reachable through
    /// the core registration adapter.
    /// </summary>
    public sealed class IntegerType : SingleType
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
    public sealed class DecimalType : SingleType
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
