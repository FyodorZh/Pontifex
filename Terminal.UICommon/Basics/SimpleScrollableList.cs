using Terminal.Gui;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Terminal.UI
{
    public class SimpleScrollableList : ListView
    {
        //public ScrollBar VScrollBar { get; private set; } 
        
        public SimpleScrollableList(View host)
        {
            host.Add(this);
            VerticalScrollBar.AutoShow = true;
            // VScrollBar = new ScrollBar()
            // {
            //     AutoShow = false
            // };
            // Add(VScrollBar);

            // bool stopRecursion = false;
            //
            // VScrollBar.PositionChanged += (s,e) => 
            // {
            //     if (stopRecursion)
            //     {
            //         return;
            //     }
            //     TopItem = VScrollBar.Position;
            //     if (TopItem != VScrollBar.Position) 
            //     {
            //         VScrollBar.Position = TopItem;
            //     }
            //
            //     SetNeedsDraw();
            // };
            //
            // DrawComplete += (s,e) =>
            // {
            //     stopRecursion = true;
            //     VScrollBar.ScrollableContentSize = Source.Count - 1;
            //     VScrollBar.Position = TopItem;
            //     VScrollBar.SetNeedsDraw();
            //     stopRecursion = false;
            // };
        }
    }
}