using Actuarius.Memory;
using Pontifex.Factory.Compilation;
using Pontifex.Factory.Registration;
using Pontifex.TransportFormula.Compilation;
using Pontifex.TransportFormula.Semantics;
using Scriba;
using TransportTypeSet = Pontifex.TransportFormula.Registry.TransportTypeSet;

namespace Pontifex.Factory
{
    public interface ITransportFactory
    {
        FormulaResolveResult Resolve(
            string formula,
            TransportTypeSet? expected = null);

        string Canonicalize(string formula);

        /// <summary>
        /// Creates a direct compiler bound to this factory's frozen runtime
        /// registry and converter graph. Callers never construct the compiler or
        /// the registry themselves.
        /// </summary>
        ITransportCompiler CreateCompiler(
            TransportRole role,
            ILogger logger,
            IMemoryRental memory);

        TOutput Compile<TOutput>(
            string formula,
            ICompiler<TOutput> compiler,
            TransportTypeSet? expected = null);
    }
}
