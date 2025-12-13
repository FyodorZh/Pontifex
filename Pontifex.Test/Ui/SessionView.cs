using Pontifex.UI;
using Scriba;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.UICommon;

namespace Pontifex.Test
{
    public class SessionView : Runnable
    {
        private static readonly string _configPath = "../../../colorscheme.json";
        
        public SessionView(TransportFactory factory)
        {
            var colorScheme = LoadScheme();

            Button.DefaultShadow = ShadowStyle.None;
            Arrangement = ViewArrangement.Fixed;
            
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
                    }})
                ])
            ];
            
            Add(menuBar);
            Add(new TransportFactoryWindow(factory));

            var loggerView = new LoggerView()
            {
                X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill()
            };
            
            Log.AddConsumer(loggerView);
            
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