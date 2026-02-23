using Pontifex.Abstractions;
using Terminal.Gui.ViewBase;
using Terminal.UI;

namespace Pontifex.Test
{
    public abstract class ControlView : LabelSetView
    {
        public IControl Control { get; }
        
        public ControlView(IControl control)
        {
            Control = control;
            Title = control.Name;
            Width = Dim.Fill();
            Height = Dim.Auto();
        }
    }
}