namespace Pontifex.Raw
{
    public interface IRawTransport : ITransport
    {
        /// <summary>
        /// Gets the maximum single-message size in bytes supported by the transport.
        /// </summary>
        int MessageMaxByteSize { get; }
    }
}