namespace Pontifex.Protocols.Reconnectable.AckReliableRaw
{
    internal abstract class Intention
    {
    }

    internal class IntentionToConnect : Intention
    {
        public bool IsFirstConnection { get; }
        
        public IntentionToConnect(bool isFirstConnection)
        {
            IsFirstConnection = isFirstConnection;
        }
    }
    
    internal class IntentionToDisconnect : Intention
    {}
}