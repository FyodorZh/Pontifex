namespace Pontifex.Raw.Reliable.Ack.Tcp
{
    public interface IRawReliableAckTcpClientDebugControl : IControl
    {
        void GracefulDisconnect();
    }
}