namespace Pontifex.Protocols.Reconnectable.AckReliableRaw
{
    internal enum ReconnectableLogicState
    {
        BeforeReconnecting,
        Reconnecting,
        Connected,
        Stopped
    }
}