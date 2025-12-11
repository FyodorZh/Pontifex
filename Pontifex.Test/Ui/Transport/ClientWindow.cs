using Terminal.Gui.ViewBase;
using Terminal.UI;

namespace Pontifex.Test
{
    public class ClientWindow : SmartWindow
    {
        public ClientWindow(TransportFactory factory, string url)
        {
            X = Pos.Center();
            Y = 1;
            Title = "Client: " + url;
            this.SetResizable(100, 10);
        }
    }
}