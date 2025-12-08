using System;
using Terminal.Gui;

namespace Terminal.UI
{
    public class ResizableWindow : Window
    {
        public int MinWidth { get; set; } = 3;
        public int MinHeight { get; set; } = 3;
        public bool AllowResize { get; set; } = true;

        int _resizeState = 0;
        int _fromX = 0;
        int _fromY = 0;
        int _fromW = 0;
        int _fromH = 0;
        Dim _fromWidth = 0;
        Dim _fromHeight = 0;


        public ResizableWindow()
        {
            Application.MouseEvent += OnMouseEvent;
            Arrangement = ViewArrangement.Movable;
        }

        protected override void Dispose(bool disposing)
        {
            Application.MouseEvent -= OnMouseEvent;
            base.Dispose(disposing);
        }

        private void OnMouseEvent(object? sender, MouseEvent evt)
        {
            if (!AllowResize)
            {
                _resizeState = 0;
                return;
            }
            if (HasFocus && (evt.Flags & MouseFlags.Button1Pressed) != 0)
            {
                var framePos = ScreenToFrame(evt.Position);
                switch (_resizeState)
                {
                    case 0:
                        if (framePos.X == Frame.Width - 1 && framePos.Y == Frame.Height - 1)
                        {
                            _resizeState = 1;
                            Arrangement = ViewArrangement.Fixed;
                            _fromX = evt.Position.X;
                            _fromY = evt.Position.Y;
                            _fromW = Frame.Width;
                            _fromH = Frame.Height;
                            _fromWidth = Dim.Absolute(Frame.Width)!;
                            _fromHeight =  Dim.Absolute(Frame.Height)!;
                        }

                        break;
                    case 1:
                        var dx = Math.Max(evt.Position.X - _fromX, MinWidth - _fromW);
                        var dy = Math.Max(evt.Position.Y - _fromY, MinHeight - _fromH);
                        Width = _fromWidth + dx;
                        Height = _fromHeight + dy;
                        break;
                }
            }
            else
            {
                if (_resizeState == 1)
                {
                    Arrangement = ViewArrangement.Movable;
                }
                _resizeState = 0;
            }
        }
    }
}