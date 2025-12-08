using System.Threading;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;
using BindingFlags = System.Reflection.BindingFlags;

namespace Terminal.UI
{
    public static class UISystem
    {
        public static void Run(Toplevel top)
        {
            var driverType = typeof(Application).Assembly.GetType("Terminal.Gui.WindowsDriver");
            //var driverType = typeof(Application).Assembly.GetType("Terminal.Gui.CursesDriver");
            
            var ctor = driverType!.GetConstructor(BindingFlags.Public | BindingFlags.Instance, []);
            var driver = ctor!.Invoke([]);
            
            Application.Init((ConsoleDriver)driver);
            

            Application.Run(top, exception =>
            {
                return true;
            } );

            Application.Shutdown();
        }
    }
}