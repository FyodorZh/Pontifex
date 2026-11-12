using Pontifex.TransportFormula.Parsing;
using Pontifex.TransportFormula.Registry;

namespace Pontifex.TransportFormula.Semantics
{
    public interface IResolver
    {
        FormulaResolveResult Resolve(
            AstFormula formula,
            TransportTypeSet expected);
    }
}
