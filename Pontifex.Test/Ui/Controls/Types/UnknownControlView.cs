using Pontifex.Abstractions;

namespace Pontifex.Test
{
    public class UnknownControlView : ControlView
    {
        public UnknownControlView(IControl control) 
            : base(control)
        {
            string name = control.GetType().Name;
            RegisterLabel("UnknownType:").Text = name;
        }
    }
}