using System.Diagnostics.CodeAnalysis;

namespace Pontifex.Factory
{
    public class DescriptionElement : BaseElement
    {
        private readonly IDescription _value;

        public DescriptionElement(IDescription value)
        {
            _value = value;
        }

        public override ElementType Type => ElementType.Description;

        public override bool EvaluateAsDescription([MaybeNullWhen(false)] out IDescription value)
        {
            value = _value;
            return true;
        }

        public override string ToString() => _value.ToString();
    }
}
