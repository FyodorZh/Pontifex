using Pontifex.Factory.Semantics;

namespace Pontifex.Factory.Compilation;

public interface ICompiler<TOutput>
{
    TOutput Compile(ResolvedFormula formula);
}
