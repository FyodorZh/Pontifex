using Pontifex.Abstractions;

namespace Pontifex.Transports.Tcp
{
    public interface IAckRawTcpClientDebugControl : IControl
    {
        void GracefulDisconnect();
    }
}