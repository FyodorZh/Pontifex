namespace Pontifex.Api.Protocol
{
    /// <summary>
    /// Represents a server-to-client raw message declaration for unidirectional communication.
    /// In server mode, this declaration allocates an input pipe to send messages to the client.
    /// In client mode, this declaration allocates an output pipe to receive messages from the server.
    /// </summary>
    public class S2CRawMessageDecl : RawMessageDecl
    {
        protected override void Prepare(bool isServerMode, IPipeAllocator pipeAllocator)
        {
            if (isServerMode)
            {
                SetPipeIn(pipeAllocator.AllocateRawPipeIn());
            }
            else
            {
                SetPipeOut(pipeAllocator.AllocateRawPipeOut());
            }
        }
    }
}