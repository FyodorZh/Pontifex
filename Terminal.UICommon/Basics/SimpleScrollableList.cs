using Terminal.Gui;

namespace Terminal.UI
{
    public class SimpleScrollableList : ListView
    {
        public ScrollBarView VScrollBar { get; private set; } 
        
        public SimpleScrollableList(View host)
        {
            host.Add(this);
            VScrollBar = new ScrollBarView(this, true, false)
            {
                AutoHideScrollBars = false
            };

            bool stopRecursion = false;
            
            VScrollBar.ChangedPosition += (s,e) => 
            {
                if (stopRecursion)
                {
                    return;
                }
                TopItem = VScrollBar.Position;
                if (TopItem != VScrollBar.Position) 
                {
                    VScrollBar.Position = TopItem;
                }
                SetNeedsDisplay ();
            };
            
            DrawContent += (s,e) =>
            {
                stopRecursion = true;
                VScrollBar.Size = Source.Count - 1;
                VScrollBar.Position = TopItem;
                VScrollBar.Refresh();
                stopRecursion = false;
            };
        }
    }
}