namespace Pontifex.Test
{
    public class RawReliableAckClientControlView : ControlView
    {
        public RawReliableAckClientControlView(IRawReliableAckClientControl control) 
            : base(control)
        {
            RegisterButton("Transport:", "Stop", control.Stop);
        }
    }
}