using Pontifex.TransportFormula.Semantics;

namespace Pontifex.TransportFormula.Compilation
{
    public interface ICompiler<TOutput>
    {
        TOutput Compile(ResolvedFormula formula);
    }
}
