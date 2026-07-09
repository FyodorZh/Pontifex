using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Pontifex.Factory
{
    public enum ElementType
    {
        Void,
        Bool,
        Long,
        Double,
        String,
        Description,
        Array
    }
    
    public interface IElement
    {
        ElementType Type { get; }
        bool EvaluateAsBool(out bool value);
        bool EvaluateAsLong(out long value);
        bool EvaluateAsDouble(out double value);
        bool EvaluateAsString(out string value);
        bool EvaluateAsDescription([MaybeNullWhen(false)] out IDescription value);
        bool EvaluateAsArray([MaybeNullWhen(false)] out IReadOnlyList<IElement> value);
    }
}