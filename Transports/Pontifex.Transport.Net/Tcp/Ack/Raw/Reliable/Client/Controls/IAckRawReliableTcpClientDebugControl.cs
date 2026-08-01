namespace Pontifex.Ack.Raw.Reliable.Tcp
{
    public interface IAckRawReliableTcpClientDebugControl : IControl
    {
        void GracefulDisconnect();
    }
}