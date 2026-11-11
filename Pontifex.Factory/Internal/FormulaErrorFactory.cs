using System;

namespace Pontifex.Factory.Internal;

internal static class FormulaErrorFactory
{
    public static FactoryError Create(
        FactoryPhase phase,
        string code,
        string message,
        int offset)
    {
        return new FactoryError(code, message, phase, new SourceSpan(offset, 0));
    }

    public static FactoryError Create(
        FactoryPhase phase,
        string code,
        string message,
        SourceSpan? span)
    {
        return new FactoryError(code, message, phase, span);
    }

    public static string JoinAlternatives(params string[] values)
    {
        if (values is null || values.Length == 0)
        {
            throw new ArgumentException("At least one alternative is required.", nameof(values));
        }

        return string.Join(" or ", values);
    }
}
