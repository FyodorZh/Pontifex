using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Pontifex.Factory
{
    public class ArrayElement : BaseElement
    {
        private readonly IReadOnlyList<IElement> _value;

        public ArrayElement(IReadOnlyList<IElement> value)
        {
            _value = value;
        }

        public override ElementType Type => ElementType.Array;

        public override bool EvaluateAsArray([MaybeNullWhen(false)] out IReadOnlyList<IElement> value)
        {
            value = _value;
            return true;
        }

        public override string ToString() => "[" + string.Join(", ", _value) + "]";
    }
}
