using Pontifex.Raw.Reliable.Ack.Tcp;

namespace Pontifex.Test
{
    public static class ControlViewFactory
    {
        public static ControlView Construct(IControl control)
        {
            return control switch
            {
                IRawReliableAckClientControl ackRawClientControl => new RawReliableAckClientControlView(ackRawClientControl),
                IRawReliableAckTcpClientDebugControl ackRawTcpClientDebugControlrol => new RawReliableAckTcpClientDebugControlView(ackRawTcpClientDebugControlrol),
                _ => new UnknownControlView(control)
            };
        }
    }
}