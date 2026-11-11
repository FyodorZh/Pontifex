using Pontifex.Factory.Compilation;
using Pontifex.Factory.Registry;
using Pontifex.Factory.Semantics;

namespace Pontifex.Factory
{
    public interface ITransportFactory
    {
        FormulaResolveResult Resolve(
            string formula,
            TransportTypeSet? expected = null);

        string Canonicalize(string formula);

        TOutput Compile<TOutput>(
            string formula,
            ICompiler<TOutput> compiler,
            TransportTypeSet? expected = null);
    }
}
