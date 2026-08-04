namespace Pontifex.Raw
{
    public interface IRawEndpoint : IBaseEndpoint
    {        
        /// <summary>
        /// Gets the remote endpoint address, or null if not connected.
        /// </summary>
        IEndPoint? RemoteEndPoint { get; }
        
        /// <summary>
        /// Gets the maximum message size in bytes supported by the transport.
        /// </summary>
        int MessageMaxByteSize { get; }
    }
}