using System;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Converters
{
    public interface ITransportConverter
    {
        TransportType From { get; }
        TransportType To { get; }

        /// <summary>
        /// Converts a transport factory from one type to another.
        /// The factory produces transports of <see cref="From"/> type;
        /// the returned transport implements <see cref="To"/> type
        /// and uses the factory internally for reconnection.
        /// </summary>
        /// <param name="innerTransportCtor">
        /// Factory that creates transports of <see cref="From"/> type.
        /// Guaranteed: <c>innerTransportCtor().Type == From</c>.
        /// </param>
        /// <param name="memoryOverride">Optional memory rental override.</param>
        /// <param name="loggerOverride">Optional logger override.</param>
        /// <returns>
        /// A factory that creates transports implementing <see cref="To"/> type.
        /// Each invocation constructs a fresh wrapper around a transport produced by
        /// <paramref name="innerTransportCtor"/>, enabling transparent reconnection.
        /// </returns>
        Func<ITransport> Convert(Func<ITransport> innerTransportCtor, IMemoryRental? memoryOverride = null, ILogger? loggerOverride = null);
    }
}