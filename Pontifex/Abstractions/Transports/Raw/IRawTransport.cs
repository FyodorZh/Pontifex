namespace Pontifex.Raw
{
    public interface IRawTransport : ITransport
    {
        /// <summary>
        /// Gets the maximum single-message size in bytes supported by the transport.
        /// Safe to read concurrently.
        /// </summary>
        int MessageMaxByteSize { get; }
    }
}