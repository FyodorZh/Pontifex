using System.Globalization;

namespace Pontifex.Factory
{
    public class StringElement : BaseElement
    {
        private readonly string _value;

        public StringElement(string value)
        {
            _value = value;
        }

        public static implicit operator StringElement(string value) => new(value);

        public override ElementType Type => ElementType.String;

        public override bool EvaluateAsString(out string value)
        {
            value = _value;
            return true;
        }

        public override bool EvaluateAsBool(out bool value)
        {
            return bool.TryParse(_value, out value);
        }

        public override bool EvaluateAsLong(out long value)
        {
            return long.TryParse(_value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public override bool EvaluateAsDouble(out double value)
        {
            return double.TryParse(_value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
