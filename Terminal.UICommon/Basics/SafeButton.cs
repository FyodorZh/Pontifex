using System;
using Terminal.Gui;

namespace Terminal.UI
{
    public sealed class SafeButton : View
    {
        private readonly Button _button;

        public new string Text
        {
            get => _button.Text;
            set => _button.Text = value;
        }
        public string WarningTitle { get; } = "Warning";
        public string WarningMessage { get; } = "Are you sure?";
        public string WarningYes {get;} = "Yes";
        public string WarningNo {get;} = "No";
        
        public new event Action? Accept;

        public SafeButton()
        {
            Width = Dim.Auto();
            _button = new()
            {
                Width = Dim.Auto()
            };
            _button.Accept += (_, _) =>
            {
                if (MessageBox.Query(0, 0, WarningTitle, WarningMessage, WarningYes, WarningNo) == 0)
                {
                    Accept?.Invoke();
                }
            };
            Add(_button);
        }
    }
}