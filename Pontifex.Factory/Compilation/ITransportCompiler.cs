using Pontifex.Factory.Registration;
using Pontifex.TransportFormula.Compilation;

namespace Pontifex.Factory.Compilation
{
    /// <summary>
    /// Direct runtime compiler: turns a resolved formula tree into live
    /// transports for one role. The role is compiler configuration, not formula
    /// data. See parsing.md §9.
    /// </summary>
    public interface ITransportCompiler : ICompiler<ITransport>
    {
        TransportRole Role { get; }
    }
}
