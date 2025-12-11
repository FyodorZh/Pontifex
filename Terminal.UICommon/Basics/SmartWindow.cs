using System;
using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Terminal.UI
{
    public class SmartWindow : Window
    {
        private int _minWidth;
        private int _minHeight;

        public SmartWindow() : this(true)
        {
        }

        public SmartWindow(bool canClose)
        {
            BorderStyle = LineStyle.Double;
            Arrangement = ViewArrangement.Movable | ViewArrangement.Overlapped;
            
            if (canClose)
            {
                var closeBtn = new Button()
                {
                    X = Pos.AnchorEnd(5),
                    Y = 0,
                    Text = "X"
                };
                closeBtn.Accepting += (sender, args) =>
                {
                    if (SuperView == null)
                    {
                        return;
                    }
                    
                    if (CanClose())
                    {
                        SuperView.Remove(this);
                        Dispose();
                    }
                    args.Handled = true;
                };
                Border.Add(closeBtn);
            }

            SetFocus();
        }

        public void SetResizable(int minWidth, int minHeight, int width = -1, int height = -1)
        {
            _minWidth = minWidth;
            _minHeight = minHeight;
            Width = Math.Max(width, minWidth);
            Height = Math.Max(height, minHeight);

            var corner = new View()
            {
                X = Pos.Align(Alignment.End),
                Y = Pos.Align(Alignment.End),
                Width = 1,
                Height = 1,
            };
            Border.Add(corner);

            Point startPos = new();
            Size startSize = new();
            bool resizing = false;
            corner.MouseEvent += (sender, args) =>
            {
                if ((args.Flags & MouseFlags.Button1Pressed) != 0)
                {
                    resizing = true;

                    X = Frame.Left;
                    Y = Frame.Top;
                    
                    startPos = args.ScreenPosition;
                    startSize = Frame.Size;
                    args.Handled = true;
                }

                if (resizing)
                {
                    args.Handled = true;
                }
            };
            Application.Mouse.MouseEvent += (sender, @event) =>
            {
                if (resizing && (@event.Flags & MouseFlags.Button1Pressed) != 0)
                {
                    int newWidth = Math.Max(minWidth, startSize.Width + (@event.Position.X - startPos.X));
                    int newHeight = Math.Max(minHeight, startSize.Height + (@event.Position.Y - startPos.Y));

                    if (Frame.Width != newWidth || Frame.Height != newHeight)
                    {
                        Width = newWidth;
                        Height = newHeight;
                    }
                    @event.Handled = true;
                }
                else
                {
                    resizing = false;
                }
            };

            Border.MouseEvent += (sender, args) =>
            {
                if (resizing)
                {
                    args.Handled = true;
                }
            };
        }

        protected virtual bool CanClose()
        {
            return true;
        }
    }
}