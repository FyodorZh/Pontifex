namespace Pontifex.Test
{
    public class AckRawReliableClientControlView : ControlView
    {
        public AckRawReliableClientControlView(IAckRawReliableClientControl control) 
            : base(control)
        {
            RegisterButton("Transport:", "Stop", control.Stop);
        }
    }
}