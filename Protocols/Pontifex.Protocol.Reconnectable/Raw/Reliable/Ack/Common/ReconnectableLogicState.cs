namespace Pontifex.Raw.Reliable.Ack.Reconnectable
{
    internal enum ReconnectableLogicState
    {
        BeforeReconnecting,
        Reconnecting,
        Connected,
        Stopped
    }
}