using System;
using Terminal.Gui;

namespace Terminal.UI
{
    public class ScrollableListElement_Label : Label, IScrollableListElement
    {
        private bool _selected;
        private string _text = "";

        private Action? _selectThis;

        public ScrollableListElement_Label()
        {
            MouseEvent += (sender, args) =>
            {
                if ((args.MouseEvent.Flags & MouseFlags.Button1Pressed) != 0)
                {
                    _selectThis?.Invoke();
                }
            };
        }
        
        public override string? Text
        {
            get => _text;
            set
            {
                _text = value ?? "";
                SetSelection(_selected);
            }
        }
        
        void IScrollableListElement.AttachTo(View parent, int y, Action selectThis)
        {
            X = 0;
            Y = y;
            _selectThis = selectThis;
            parent.Add(this);
        }

        void IScrollableListElement.DeAttach()
        {
            _selectThis = null;
        }
        
        bool IScrollableListElement.Selected
        {
            get => _selected;
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    SetSelection(value);
                }
            }
        }

        int IScrollableListElement.Width => Frame.Width;
        int IScrollableListElement.Height => Frame.Height;
        View IScrollableListElement.View => this;

        private void SetSelection(bool selection)
        {
            if (selection)
            {
                base.Text = "<" + _text + ">";
            }
            else
            {
                base.Text = " " + _text + " ";
            }
        }
    }
}