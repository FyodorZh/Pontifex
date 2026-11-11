using Pontifex.Factory.Semantics;

namespace Pontifex.Factory.Canonicalization
{
    public interface ICanonicalizer
    {
        string Canonicalize(ResolvedFormula formula);
    }
}
