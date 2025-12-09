using Pontifex.UI;
using Scriba;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.UICommon;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace Pontifex.Test
{
    public class SessionView : Window
    {
        private static readonly string _configPath = "../../../colorscheme.json";
        
        public SessionView()
        {
            var colorScheme = LoadScheme();
            
            Border!.Thickness = new Thickness(0, 0, 0, 0);
            Title = "Session View";
            var menuBar = new MenuBar();
            menuBar.Menus =
            [
                new MenuBarItem("_File", [
                    new MenuItem("_GC.Collect", "", () => { GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive); }),
                    new MenuItem("_Palette Constructor", "", () =>
                    {
                        Add(new PaletteConstructor(colorScheme, SaveSchemeInfo));
                    }),
                    new MenuItem("_Quit", "", () => { App!.RequestStop(); })
                ]),
                new MenuBarItem("_TMP", [
                    new MenuItem("abc", "", () => {
                    {
                        for (int i = 0; i < 10; ++i) Log.i("Hello " + i);
                        var newScheme = new Scheme(GetScheme()) with
                        {
                            Normal = new Attribute(new Color(255, 0, 0), new Color(0, 255, 0))
                        };
                        this.SetScheme(newScheme);
                    }})
                ])
            ];
            
            Add(menuBar);

            var loggerView = new LoggerView()
            {
                X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill()
            };
            
            Log.AddConsumer(loggerView, true);
            
            Add(loggerView);
        }

        private ColorSchemeInfo LoadScheme()
        {
            ColorSchemeInfo info = new ColorSchemeInfo();
            if (File.Exists(_configPath))
            {
                info.ImportFromJson(File.ReadAllText(_configPath));
                info.SaveToSchemeManager();
            }

            info.LoadFromSchemeManager(false);
            return info;
        }

        public void SaveSchemeInfo(ColorSchemeInfo info)
        {
            File.WriteAllText(_configPath, info.ExportToJson());
        }
    }
}