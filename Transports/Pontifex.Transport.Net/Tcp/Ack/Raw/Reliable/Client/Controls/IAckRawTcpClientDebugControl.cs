namespace Pontifex.Ack.Raw.Reliable.Tcp
{
    public interface IAckRawTcpClientDebugControl : IControl
    {
        void GracefulDisconnect();
    }
}