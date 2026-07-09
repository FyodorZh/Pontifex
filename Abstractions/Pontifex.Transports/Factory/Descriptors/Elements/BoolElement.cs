namespace Pontifex.Factory
{
    public class BoolElement : BaseElement
    {
        private readonly bool _value;

        public BoolElement(bool value)
        {
            _value = value;
        }

        public static implicit operator BoolElement(bool value) => new(value);

        public override ElementType Type => ElementType.Bool;

        public override bool EvaluateAsBool(out bool value)
        {
            value = _value;
            return true;
        }
    }
}
