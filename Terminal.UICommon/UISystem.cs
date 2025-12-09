using Terminal.Gui.App;
using Terminal.Gui.Configuration;

namespace Terminal.UI
{
    public static class UISystem
    {
        public static void Init()
        {
            ConfigurationManager.RuntimeConfig = """{ "Theme": "Default" }""";
            ConfigurationManager.Enable (ConfigLocations.All);
        }
        
        public static void Run(IRunnable runnable)
        {
            Application.Init("dotnet");
            Application.Run(runnable);
            Application.Shutdown();
           // using IApplication app = Application.Create();
           // app.Init("dotnet");
           // app.Run(runnable);
        }
    }
}