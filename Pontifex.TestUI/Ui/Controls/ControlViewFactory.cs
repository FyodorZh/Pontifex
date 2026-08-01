using Pontifex.Ack.Raw.Reliable.Tcp;

namespace Pontifex.Test
{
    public static class ControlViewFactory
    {
        public static ControlView Construct(IControl control)
        {
            return control switch
            {
                IAckRawReliableClientControl ackRawClientControl => new AckRawReliableClientControlView(ackRawClientControl),
                IAckRawReliableTcpClientDebugControl ackRawTcpClientDebugControlrol => new AckRawReliableTcpClientDebugControlView(ackRawTcpClientDebugControlrol),
                _ => new UnknownControlView(control)
            };
        }
    }
}