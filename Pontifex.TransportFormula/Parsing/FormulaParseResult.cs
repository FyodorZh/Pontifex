using Pontifex.TransportFormula;

namespace Pontifex.TransportFormula.Parsing
{
    public sealed record FormulaParseResult(
        AstFormula? Formula,
        FactoryError? Error);
}
