namespace Pontifex.Factory
{
    public class DoubleElement : BaseElement
    {
        private readonly double _value;

        public DoubleElement(double value)
        {
            _value = value;
        }

        public static implicit operator DoubleElement(double value) => new(value);

        public override ElementType Type => ElementType.Double;

        public override bool EvaluateAsDouble(out double value)
        {
            value = _value;
            return true;
        }

        public override string ToString() => _value.ToString("G");
    }
}
