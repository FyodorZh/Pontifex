using System;
using System.Collections.Generic;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Terminal.UICommon
{
    public class RadioButtonGroup : FrameView
    {
        public event Action<CheckBox>? CheckBoxActivated;
        
        private readonly List<CheckBox> _checkBoxes = new();
        private bool _internalChanging;
        
        public RadioButtonGroup(params CheckBox[] boxes)
        {
            foreach (var box in boxes)
            {
                if (box.RadioStyle == false || box.Data != null)
                {
                    throw new InvalidOperationException();
                }
                
                Add(box);
                _checkBoxes.Add(box);

                box.CheckedStateChanging += OnBoxOnCheckedStateChanging;
                box.CheckedStateChanged += OnBoxOnCheckedStateChanged;
            }
        }

        private void OnBoxOnCheckedStateChanging(object? sender, ResultEventArgs<CheckState> args)
        {
            if (args.Result == CheckState.UnChecked && !_internalChanging)
            {
                args.Handled = true;
            }
        }

        private void OnBoxOnCheckedStateChanged(object? sender, EventArgs<CheckState> args)
        {
            if (!_internalChanging)
            {
                _internalChanging = true;
                if (args.Value == CheckState.Checked)
                {
                    foreach (var box in _checkBoxes)
                    {
                        if (box != sender)
                        {
                            box.CheckedState = CheckState.UnChecked;
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("Impossible code path");
                }
                _internalChanging = false;
                
                CheckBoxActivated?.Invoke((CheckBox)sender!);
            }
        }
    }
}