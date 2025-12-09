using System;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.UICommon
{
    public class ColorPair
    {
        public Color Foreground { get; private set; }

        public Color Background { get; private set; }

        public event Action? Updated;

        public void Update(Color foreground, Color background, bool updateEvent)
        {
            bool up = updateEvent && (Foreground.Rgba != foreground.Rgba ||  Background.Rgba != background.Rgba);
            Foreground = foreground;
            Background = background;
            if (up)
            {
                Updated?.Invoke();
            }
        }
        
        public Attribute ToAttribute() => new(Foreground, Background);

        public void LoadFrom(Attribute attr, bool updateEvent)
        {
            Update(attr.Foreground, attr.Background, updateEvent);
        }
    }
}