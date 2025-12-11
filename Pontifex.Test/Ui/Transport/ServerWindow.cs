using Scriba;
using Terminal.UI;
using Trader.Utils;
using Transport.Abstractions.Servers;
using TransportAnalyzer.TestLogic;

namespace Pontifex.Test
{
    public class ServerWindow : SmartWindow
    {
        private readonly IAckRawServer? _server;
        
        public ServerWindow(TransportFactory factory, string url)
        {
            X = 0;
            Y = 1;
            Title = "Server: " + url;
            SetResizable(100, 10);

            _server = factory.ConstructServer(url);
            if (_server != null)
            {
                _server.Init(new AckRawServerLogic());
                
                //ILogger logger = new  
                
                _server.Start(stopReason =>
                {
                    
                }, StaticLogger.Instance.Wrap("Server", url));
            }
        }

        private void Close()
        {
            if (_server != null)
            {
                _server.Stop();
            }
        }
    }
}