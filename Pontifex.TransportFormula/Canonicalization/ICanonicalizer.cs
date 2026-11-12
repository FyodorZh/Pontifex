using Pontifex.TransportFormula.Semantics;

namespace Pontifex.TransportFormula.Canonicalization
{
    public interface ICanonicalizer
    {
        string Canonicalize(ResolvedFormula formula);
    }
}
