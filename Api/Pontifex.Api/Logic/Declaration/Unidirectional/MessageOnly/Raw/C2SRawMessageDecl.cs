namespace Pontifex.Api
{
    /// <summary>
    /// Represents a client-to-server raw message declaration for unidirectional communication.
    /// In server mode, this declaration allocates an output pipe to receive messages from the client.
    /// In client mode, this declaration allocates an input pipe to send messages to the server.
    /// </summary>
    public class C2SRawMessageDecl : RawMessageDecl
    {
        protected override void Start(bool isServerMode, IPipeSystem pipeSystem)
        {
            if (isServerMode)
            {
                SetPipeOut(pipeSystem.AllocateRawPipeOut());
            }
            else
            {
                SetPipeIn(pipeSystem.AllocateRawPipeIn());
            }
        }
    }
}