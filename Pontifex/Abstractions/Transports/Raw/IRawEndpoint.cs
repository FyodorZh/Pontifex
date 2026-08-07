namespace Pontifex.Raw
{
    public interface IRawEndpoint : IBaseEndpoint
    {        
        /// <summary>
        /// Gets the remote endpoint address, or null if not connected.
        /// Safe to read concurrently.
        /// </summary>
        IEndPoint? RemoteEndPoint { get; }
        
        /// <summary>
        /// Gets the maximum message size in bytes supported by the transport.
        /// An inclusive maximum for the application payload; it excludes transport
        /// framing and control metadata. Empty payloads are valid.
        /// Safe to read concurrently.
        /// </summary>
        int MessageMaxByteSize { get; }
    }
}