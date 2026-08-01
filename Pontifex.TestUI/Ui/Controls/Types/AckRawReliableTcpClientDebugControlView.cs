using Pontifex.Ack.Raw.Reliable.Tcp;

namespace Pontifex.Test
{
    public class AckRawReliableTcpClientDebugControlView : ControlView
    {
        public AckRawReliableTcpClientDebugControlView(IAckRawReliableTcpClientDebugControl control) 
            : base(control)
        {
            RegisterButton("Transport:", "Disconnect", control.GracefulDisconnect);
        }
    }
}