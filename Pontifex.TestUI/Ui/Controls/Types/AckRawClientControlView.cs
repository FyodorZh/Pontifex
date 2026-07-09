namespace Pontifex.Test
{
    public class AckRawClientControlView : ControlView
    {
        public AckRawClientControlView(IAckRawClientControl control) 
            : base(control)
        {
            RegisterButton("Transport:", "Stop", control.Stop);
        }
    }
}