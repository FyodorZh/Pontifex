using System;
using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Terminal.UI
{
    public interface IScrollableListElement : IDisposable
    {
        void AttachTo(View parent, int y, Action selectThis);
        void DeAttach();
        bool Selected { get; set; }
        int Width { get; }
        int Height { get; }
        View View { get; }
    }

    public class ScrollableListElement<TView> : IScrollableListElement
        where TView : View
    {
        private readonly TView _view;
        private readonly View topViewCover;
        private bool _selected;

        private Action? _selectThis;
        
        public Action<TView, bool>? SelectionApplicator { get; }
        
        public ScrollableListElement(TView view, Action<TView, bool>? selectionApplicator = null)
        {
            _view = view;
            _selected = false;
            SelectionApplicator = selectionApplicator;
            //Width = _view.Frame.Width;
            Height = _view.Frame.Height;

            topViewCover = new View()
            {
                X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), 
                ViewportSettings = ViewportSettingsFlags.ClearContentOnly,
                ContentSizeTracksViewport = false
            };
            topViewCover.SetContentSize(new Size(0, 0));
            view.Add(topViewCover);
            
            topViewCover.MouseEvent += (_, args) =>
            {
                if ((args.Flags & MouseFlags.Button1Pressed) != 0)
                {
                    _selectThis?.Invoke();
                }
            };
        }

        public void AttachTo(View parent, int y, Action selectThis)
        {
            _view.X = 0;
            _view.Y = y;
            _selectThis = selectThis;

            parent.Add(_view);
        }

        public void DeAttach()
        {
            _selectThis = null;
        }

        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    SelectionApplicator?.Invoke(_view, value);
                }

                topViewCover.Visible = !value;
            }
        }

        public int Width => _view.Frame.Width;
        public int Height { get; }
        public View View => _view;

        public void Dispose()
        {
            _view.Dispose();
        }
    }
}