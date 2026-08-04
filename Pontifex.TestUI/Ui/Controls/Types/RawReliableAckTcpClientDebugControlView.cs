using Pontifex.Raw.Reliable.Ack.Tcp;

namespace Pontifex.Test
{
    public class RawReliableAckTcpClientDebugControlView : ControlView
    {
        public RawReliableAckTcpClientDebugControlView(IRawReliableAckTcpClientDebugControl control) 
            : base(control)
        {
            RegisterButton("Transport:", "Disconnect", control.GracefulDisconnect);
        }
    }
}