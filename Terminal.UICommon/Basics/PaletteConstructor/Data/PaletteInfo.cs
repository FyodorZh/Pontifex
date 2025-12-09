using System.Collections.Generic;
using System.Linq;
using Terminal.Gui.Drawing;

namespace Terminal.UICommon
{
    public class PaletteInfo
    {
        private static readonly string[] _fields =
        [
            "Normal",
            "HotNormal",
            "Focus",
            "HotFocus",
            "Active",
            "HotActive",
            "Highlight",
            "Editable",
            "ReadOnly",
            "Disabled"
        ];
        
        public static IReadOnlyList<string> Aspects => _fields;

        private readonly Dictionary<string, ColorPair> _colors = new Dictionary<string, ColorPair>(_fields.Select(f => new KeyValuePair<string, ColorPair>(f, new ColorPair())));

        public ColorPair this[string field] => _colors[field];

        public void LoadFrom(Scheme scheme, bool updateEvent)
        {
            _colors["Normal"].LoadFrom(scheme.Normal, updateEvent);
            _colors["HotNormal"].LoadFrom(scheme.HotNormal, updateEvent);
            _colors["Focus"].LoadFrom(scheme.Focus, updateEvent);
            _colors["HotFocus"].LoadFrom(scheme.HotFocus, updateEvent);
            _colors["Active"].LoadFrom(scheme.Active, updateEvent);
            _colors["HotActive"].LoadFrom(scheme.HotActive, updateEvent);
            _colors["Highlight"].LoadFrom(scheme.Highlight, updateEvent);
            _colors["Editable"].LoadFrom(scheme.Editable, updateEvent);
            _colors["ReadOnly"].LoadFrom(scheme.ReadOnly, updateEvent);
            _colors["Disabled"].LoadFrom(scheme.Disabled, updateEvent);
        }

        public Scheme ToScheme() => new Scheme()
        {
            Normal = _colors["Normal"].ToAttribute(),
            HotNormal = _colors["HotNormal"].ToAttribute(),
            Focus = _colors["Focus"].ToAttribute(),
            HotFocus = _colors["HotFocus"].ToAttribute(),
            Active = _colors["Active"].ToAttribute(),
            HotActive = _colors["HotActive"].ToAttribute(),
            Highlight = _colors["Highlight"].ToAttribute(),
            Editable = _colors["Editable"].ToAttribute(),
            ReadOnly = _colors["ReadOnly"].ToAttribute(),
            Disabled = _colors["Disabled"].ToAttribute(),
        };
    }
}