namespace Pontifex.Ack.Raw.Reliable.Reconnectable
{
    internal enum ReconnectableLogicState
    {
        BeforeReconnecting,
        Reconnecting,
        Connected,
        Stopped
    }
}