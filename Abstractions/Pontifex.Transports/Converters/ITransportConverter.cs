using Actuarius.Memory;
using Scriba;

namespace Pontifex.Converters
{
    public interface ITransportConverter
    {
        TransportType From { get; }
        TransportType To { get; }

        /// <summary>
        /// Server or Client transport converter. Converts transport from one type to another.
        /// </summary>
        /// <param name="transport">The transport instance to be converted.</param>
        /// <param name="memoryOverride">Optional memory rental for the conversion process.</param>
        /// <param name="loggerOverride">Optional logger for logging conversion details.</param>
        /// <returns>The converted transport instance.</returns>
        ITransport Convert(ITransport transport, IMemoryRental? memoryOverride = null, ILogger? loggerOverride = null);
    }
}