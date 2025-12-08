using System;
using System.Collections.Generic;
using System.Drawing;
using Terminal.Gui;

namespace Terminal.UI
{
    public sealed class ScrollableList : ScrollView
    {
        private readonly List<IScrollableListElement> _elements = new();
        private readonly View _content;
        private Size _totalSize;
        private int _selected = -1;

        public Action<int, bool>? SelectionChanged;
        public Action<IScrollableListElement, bool>? SelectionChanged2;

        public IReadOnlyList<IScrollableListElement> Elements => _elements;

        public int Selected
        {
            get => _selected;
            private set
            {
                if (_selected != value)
                {
                    if (_selected != -1)
                    {
                        _elements[_selected].Selected = false;
                        SelectionChanged?.Invoke(_selected, false);
                        SelectionChanged2?.Invoke(_elements[_selected], false);
                    }

                    if (value != -1)
                    {
                        _elements[value].Selected = true;
                        SelectionChanged?.Invoke(value, true);
                        SelectionChanged2?.Invoke(_elements[value], true);
                    }

                    _selected = value;
                }
            }
        }
        
        public ScrollableList()
        {
            _content = new View();
            _content.CanFocus = true;
            _content.Width = Dim.Fill();
            _content.Height = Dim.Absolute(0);

            _content.MouseEvent += MouseProcessor;

            Add(_content);
            
            ShowVerticalScrollIndicator = true;
            ShowHorizontalScrollIndicator = false;
        }

        public void ContentClear(bool disposeAll = true)
        {
            if (_selected != -1)
            {
                Selected = -1;
            }

            foreach (var element in _elements)
            {
                element.DeAttach();
            }
            
            _content.RemoveAll();

            if (disposeAll)
            {
                foreach (var element in _elements)
                {
                    element.Dispose();
                }
            }
            _elements.Clear();
            
            //_content.Width = Dim.Absolute(0);
            _content.Height = Dim.Absolute(0);
            _totalSize = new Size(Frame.Width - 3, 0);
            SetContentSize(_totalSize);
        }

        public void ContentAdd(IScrollableListElement element)
        {
            int id = _elements.Count;
            element.AttachTo(_content, _totalSize.Height, () =>
            {
                SetFocus();
                Selected = id;
            });
            
            
            _elements.Add(element);
            
            _totalSize.Height += element.Height;
            //_totalSize.Width = Math.Max(_totalSize.Width, element.Width);
            
            //_content.Width = _totalSize.Width;
            _content.Height = _totalSize.Height;

            Size wholeSize = new Size(_totalSize.Width, _totalSize.Height + 1);
            
            SetContentSize(wholeSize);
        }

        private void MouseProcessor(object? sender, MouseEventEventArgs args)
        {
            if ((args.MouseEvent.Flags & MouseFlags.Button1Pressed) != 0 ||
                (args.MouseEvent.Flags & MouseFlags.Button1Clicked) != 0)
            {
                var pos = this.ScreenToContent(args.MouseEvent.ScreenPosition);
                if (pos.X >= 0 && pos.X < Frame.Width - 1 &&
                    pos.Y >= 0 && pos.Y < Frame.Height - 1)
                {
                    SetFocus();
                    var posY = pos.Y - ContentOffset.Y;
                    for (int i = 0; i < _elements.Count; ++i)
                    {
                        int lineHeight = _elements[i].Height;
                        if (posY < lineHeight)
                        {
                            Selected = i;
                            break;
                        }

                        posY -= lineHeight;
                    }
                }
            }
        }
    }
}