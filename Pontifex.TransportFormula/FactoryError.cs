namespace Pontifex.TransportFormula
{
    public enum FactoryPhase
    {
        Lexing,
        Parsing,
        Resolution,
        Registration,
        Printing,
        Compilation,
        Building
    }

    public sealed record FactoryError(
        string Code,
        string Message,
        FactoryPhase Phase,
        SourceSpan? Span);
}
