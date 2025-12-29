using Pontifex.Abstractions;

namespace Pontifex.Test
{
    public static class ControlViewFactory
    {
        public static ControlView Construct(IControl control)
        {
            return control switch
            {
                IAckRawClientControl ackRawClientControl => new AckRawClientControlView(ackRawClientControl),
                _ => new UnknownControlView(control)
            };
        }
    }
}