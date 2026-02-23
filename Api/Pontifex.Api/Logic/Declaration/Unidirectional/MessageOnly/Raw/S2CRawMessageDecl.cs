namespace Pontifex.Api
{
    /// <summary>
    /// Represents a server-to-client raw message declaration for unidirectional communication.
    /// In server mode, this declaration allocates an input pipe to send messages to the client.
    /// In client mode, this declaration allocates an output pipe to receive messages from the server.
    /// </summary>
    public class S2CRawMessageDecl : RawMessageDecl
    {
        protected override void Start(bool isServerMode, IPipeSystem pipeSystem)
        {
            if (isServerMode)
            {
                SetPipeIn(pipeSystem.AllocateRawPipeIn());
            }
            else
            {
                SetPipeOut(pipeSystem.AllocateRawPipeOut());
            }
        }
    }
}