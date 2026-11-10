using Pontifex.Factory.Parsing;
using Pontifex.Factory.Registry;

namespace Pontifex.Factory.Semantics;

public interface IResolver
{
    FormulaResolveResult Resolve(
        AstFormula formula,
        TransportTypeSet expected);
}
