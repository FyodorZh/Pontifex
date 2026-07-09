namespace Pontifex.Factory
{
    public class LongElement : BaseElement
    {
        private readonly long _value;

        public LongElement(long value)
        {
            _value = value;
        }

        public static implicit operator LongElement(long value) => new(value);

        public override ElementType Type => ElementType.Long;

        public override bool EvaluateAsLong(out long value)
        {
            value = _value;
            return true;
        }

        public override bool EvaluateAsDouble(out double value)
        {
            value = _value;
            return true;
        }
    }
}
