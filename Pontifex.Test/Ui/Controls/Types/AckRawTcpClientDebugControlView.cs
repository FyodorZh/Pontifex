using Pontifex.Transports.Tcp;

namespace Pontifex.Test
{
    public class AckRawTcpClientDebugControlView : ControlView
    {
        public AckRawTcpClientDebugControlView(IAckRawTcpClientDebugControl control) 
            : base(control)
        {
            RegisterButton("Transport:", "Disconnect", control.GracefulDisconnect);
        }
    }
}