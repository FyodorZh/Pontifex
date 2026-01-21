using Actuarius.Memory;
using Pontifex.UI;
using Scriba;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.UI;
using Pontifex.Abstractions.Servers;
using Pontifex.Api;
using TransportAnalyzer.TestLogic;

namespace Pontifex.Test
{
    public class ServerWindow : SmartWindow
    {
        private readonly IAckRawServer? _server;
        private readonly ILogger _logger;
        
        public ServerWindow(TransportFactory factory, string url, bool startApi)
        {
            X = 0;
            Y = 1;
            Title = "Server: " + url;
            SetResizable(150, 20);

            LoggerView loggerView = new()
            {
                X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(1),
                BorderStyle = LineStyle.Rounded
            };
            Add(loggerView);

            _logger = new Logger([loggerView]);  

            _server = factory.ConstructServer(url, _logger, MemoryRental.Shared);
            if (startApi)
            {
                ServerSideApiFactory<AckRawProtocol_Server> apiFactory = new ServerSideApiFactory<AckRawProtocol_Server>(
                    () => new AckRawProtocol_Server(MemoryRental.Shared, _logger), MemoryRental.Shared, _logger);
                
                if (_server == null || !_server.Init(apiFactory))
                {
                    _logger.e("Failed to construct server");
                    return;
                }
            }
            else
            {
                if (_server == null || !_server.Init(new AckRawServerLogic(_server.Log, _server.Memory)))
                {
                    _logger.e("Failed to construct server");
                    return;
                }    
            }
            

            bool started = _server.Start(stopReason =>
            {
                _logger.e("OnStopped: " + stopReason);
            });
            _logger.i($"Starting: {(started ? "OK" : "FAILED")}");

            Button stopButton = new()
            {
                X = 0, Y = Pos.Bottom(loggerView), Width = Dim.Auto(), Height = 1, Title = "Transport.Stop"
            };
            Add(stopButton);
            stopButton.Accepting += (sender, args) =>
            {
                _server?.Stop();
                args.Handled = true;
            };
        }

        protected override bool CanClose()
        {
            bool canClose = _server == null || !_server.IsStarted;
            if (!canClose)
            {
                _logger.e("Can't close window. Stop server before");
            }
            return canClose;
        }

        protected override void Dispose(bool disposing)
        {
            _server?.Stop();
            //_logger.Dispose();
            base.Dispose(disposing);
        }
    }
}