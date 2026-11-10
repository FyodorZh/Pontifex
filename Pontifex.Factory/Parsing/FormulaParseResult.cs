using Pontifex.Factory;

namespace Pontifex.Factory.Parsing;

public sealed record FormulaParseResult(
    AstFormula? Formula,
    FactoryError? Error);
