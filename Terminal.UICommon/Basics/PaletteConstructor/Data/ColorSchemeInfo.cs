using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;

namespace Terminal.UICommon
{
    public class ColorSchemeInfo
    {
        public static IEnumerable<Schemes> SchemesList
        {
            get
            {
                for (Schemes i = Schemes.Base; i <= Schemes.Error; ++i)
                {
                    yield return i;
                }
            }
        }
        
        private readonly Dictionary<Schemes, PaletteInfo> _palettes = new Dictionary<Schemes, PaletteInfo>();

        public ColorSchemeInfo()
        {
            foreach (var i in SchemesList)
            {
                _palettes.Add(i, new PaletteInfo());
            }
        }
        
        public PaletteInfo this[Schemes scheme] => _palettes[scheme];

        public void LoadFromSchemeManager(bool updateEvent)
        {
            foreach (var kv in _palettes)
            {
                var scheme = SchemeManager.GetScheme(kv.Key);
                kv.Value.LoadFrom(scheme, updateEvent);
            }
        }

        public void SaveToSchemeManager()
        {
            foreach (var kv in _palettes)
            {
                SchemeManager.AddScheme(SchemeManager.SchemesToSchemeName(kv.Key)!,  kv.Value.ToScheme());
            }
        }

        public (string schemeName, string paletteName, Color foreground, Color background)[] Export()
        {
            List<(string schemeName, string paletteName, Color foreground, Color background)> list = new();
            foreach (var kv in _palettes)
            {
                foreach (var p in PaletteInfo.Aspects)
                {
                    list.Add((kv.Key.ToString(), p, kv.Value[p].Foreground, kv.Value[p].Background));
                }
            }
            return list.ToArray();
        }
        
        public void Import((string schemeName, string paletteName, Color foreground, Color background)[] data, bool updateEvent)
        {
            foreach (var entity in data)
            {
                var pair = _palettes[Enum.Parse<Schemes>(entity.schemeName)][entity.paletteName];
                pair.Update(entity.foreground, entity.background, updateEvent);
            }
        }

        public string ExportToJson()
        {            
            var list = Export().Select(e => new DataRecord()
            {
                SchemeName = e.schemeName,
                PaletteName = e.paletteName,
                Foreground = e.foreground.Rgba,
                Background = e.background.Rgba
            }).ToArray();
            
            return JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        }

        public void ImportFromJson(string json)
        {
            var list = JsonSerializer.Deserialize<DataRecord[]>(json);
            var typedList = list?.Select(element => (element.SchemeName, element.PaletteName, 
                new Color(element.Foreground), new Color(element.Background))).ToArray() ?? [];
            Import(typedList, true);
        }

        private struct DataRecord
        {
            public string SchemeName { get; set; }
            public string PaletteName { get; set; }
            public int Foreground { get; set; }
            public int Background { get; set; }
        }
    }
}