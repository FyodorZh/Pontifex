using System.Collections.Generic;

namespace Pontifex.Factory.Registration
{
    /// <summary>
    /// Describes and constructs a transport for the Transport Formula factory.
    /// Transport packages implement this interface; the factory consumes it to
    /// register schemes, parameters, and aliases, and calls
    /// <see cref="Create"/> to build live transports.
    /// </summary>
    public interface ITransportRegistration
    {
        /// <summary>
        /// Canonical scheme name. Compared case-insensitively and normalized to
        /// lowercase by the factory.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Fixed transport type produced by <see cref="Create"/>.
        /// </summary>
        TransportType Type { get; }

        /// <summary>
        /// Parameters in declaration order.
        /// </summary>
        IReadOnlyList<TransportParameter> Parameters { get; }

        /// <summary>
        /// Alias scheme names proposed by the transport. May be empty.
        /// </summary>
        IReadOnlyList<string> Aliases { get; }

        /// <summary>
        /// Creates the live transport. Any failure is wrapped by the compiler as
        /// <c>E_BLD_CONSTRUCTOR_FAILED</c>. Nested transports created through the
        /// context are owned by this registration once created.
        /// </summary>
        ITransport Create(ITransportBuildContext context);
    }
}
