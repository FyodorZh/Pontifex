namespace Pontifex.Protocols.Reconnectable.AckReliableRaw
{
    internal abstract class Intention
    {
    }

    internal class IntentionToConnect(bool isFirstConnection) : Intention
    {
        public bool IsFirstConnection { get; } = isFirstConnection;
    }
    
    internal class IntentionToDisconnect : Intention
    {}
}