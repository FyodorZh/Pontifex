using Pontifex.Abstractions;
using Pontifex.Transports.Tcp;

namespace Pontifex.Test
{
    public static class ControlViewFactory
    {
        public static ControlView Construct(IControl control)
        {
            return control switch
            {
                IAckRawClientControl ackRawClientControl => new AckRawClientControlView(ackRawClientControl),
                IAckRawTcpClientDebugControl ackRawTcpClientDebugControlrol => new AckRawTcpClientDebugControlView(ackRawTcpClientDebugControlrol),
                _ => new UnknownControlView(control)
            };
        }
    }
}