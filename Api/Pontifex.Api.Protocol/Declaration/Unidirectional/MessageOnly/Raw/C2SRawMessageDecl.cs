namespace Pontifex.Api.Protocol
{
    /// <summary>
    /// Represents a client-to-server raw message declaration for unidirectional communication.
    /// In server mode, this declaration allocates an output pipe to receive messages from the client.
    /// In client mode, this declaration allocates an input pipe to send messages to the server.
    /// </summary>
    public class C2SRawMessageDecl : RawMessageDecl
    {
        protected override void Prepare(bool isServerMode, IPipeAllocator pipeAllocator)
        {
            if (isServerMode)
            {
                SetPipeOut(pipeAllocator.AllocateRawPipeOut());
            }
            else
            {
                SetPipeIn(pipeAllocator.AllocateRawPipeIn());
            }
        }
    }
}