namespace Pontifex.Test
{
    public class RawReliableClientControlView : ControlView
    {
        public RawReliableClientControlView(IRawReliableClientControl control) 
            : base(control)
        {
            RegisterButton("Transport:", "Stop", control.Stop);
        }
    }
}
