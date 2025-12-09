using System;
using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Terminal.UI
{
    public class LabelSetView : FrameView
    {
        private readonly VerticalLayout _labels;
        private readonly VerticalLayout _values;

        public Size Size => new Size(_labels.Size.Width + _values.Size.Width,
            Math.Max(_labels.Size.Height, _values.Size.Height));

        public LabelSetView(bool showBorder = true)
        {
            Width = Dim.Auto();
            Height = Dim.Auto();
            BorderStyle = showBorder ? LineStyle.Single : LineStyle.None;
            
            _labels = new VerticalLayout()
            {
                X = 0,
                Y = 0,
                Width = Dim.Auto(),
                Height = Dim.Auto()
            };

            _values = new VerticalLayout()
            {
                X = Pos.Right(_labels),
                Y = 0,
                Width = Dim.Auto(),
                Height = Dim.Auto()
            };

            Add(_labels, _values);
        }
        
        public View GetRightBottomView()
        {
            return _values;
        }
        
        public Label RegisterLabel(string caption, int? width = null)
        {
            _labels.ContentAdd(new Label()
            {
                Width = Dim.Auto(),
                Text = caption
            });

            var value = new Label()
            {
                Width = width ?? Dim.Auto(),
                Text = "???"
            };
            _values.ContentAdd(value);

            value.TextChanged += (sender, args) =>
            {
                _values.RefreshSize();
            };

            return value;
        }
        
        public Button RegisterButton(string text, string caption, Action onEnter)
        {
            _labels.ContentAdd(new Label()
            {
                Width = Dim.Auto(),
                Text = text
            });

            var value = new Button()
            {
                Width = Dim.Auto(),
                Text = caption
            };
            _values.ContentAdd(value);

            value.Accepting += (sender, args) =>
            {
                onEnter();
                args.Handled = true;
            };

            return value;
        }
        
        public TextField RegisterField(string caption, string text, int textWidth, bool showBtn, Action<string> textChanged)
        {
            _labels.ContentAdd(new Label()
            {
                Width = Dim.Auto(),
                Text = caption
            });

            TextField textField;
            if (showBtn)
            {
                const int btnWidth = 7;
                var view = new View()
                {
                    Width = textWidth + btnWidth + 2,
                    Height = 1
                };
                textField = new TextField()
                {
                    X = 0,
                    Width = textWidth,
                    Text = text
                };
                var button = new Button()
                {
                    X = textWidth + 1,
                    Width = btnWidth,
                    Text = "SET"
                };

                view.Add(textField, button);
                _values.ContentAdd(view);

                button.Accepting += (sender, args) =>
                {
                    textChanged.Invoke(text);
                    args.Handled = true;
                };
            }
            else
            {
                textField = new TextField()
                {
                    Width = textWidth,
                    Text = text
                };
                _values.ContentAdd(textField);
            }

            textField.TextChanged += (sender, args) =>
            {
                text = textField.Text;
                if (!showBtn)
                {
                    textChanged(text);
                }
            };

            return textField;
        }

        public void AddSpace()
        {
            _labels.ContentAdd(new View()
            {
                Width = 1,
                Height = 1
            });
            _values.ContentAdd(new View()
            {
                Width = 1,
                Height = 1
            });
        }

        public TView RegisterAny<TView>(string caption, TView view)
            where TView : View
        {
            _labels.ContentAdd(new Label()
            {
                Width = Dim.Auto(),
                Text = caption
            });

            _values.ContentAdd(view);
            return view;
        }
    }
}