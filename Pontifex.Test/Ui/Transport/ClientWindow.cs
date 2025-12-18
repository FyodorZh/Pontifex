using Actuarius.Memory;
using Pontifex.UI;
using Scriba;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.UI;
using Pontifex.Abstractions.Clients;
using TransportAnalyzer.TestLogic;

namespace Pontifex.Test
{
    public class ClientWindow : SmartWindow
    {
        private readonly IAckRawClient? _client;
        private readonly ILogger _logger;
        
        public ClientWindow(TransportFactory factory, string url)
        {
            X = Pos.Center();
            Y = 1;
            Title = "Client: " + url;
            this.SetResizable(200, 20);
            
            LoggerView loggerView = new()
            {
                X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(1),
                BorderStyle = LineStyle.Rounded
            };
            Add(loggerView);

            _logger = new Logger(loggerView);  
            
            
            _client = factory.ConstructClient(url, _logger, MemoryRental.Shared);
            if (_client == null || !_client.Init(new AckRawClientLogic(10, 100000)))
            {
                _logger.e("Failed to construct client");
                return;
            }

            bool started = _client.Start(stopReason =>
            {
                _logger.e("OnStopped: " + stopReason);
            });
            _logger.i($"Starting: {(started ? "OK" : "FAILED")}");

            Button stopButton = new()
            {
                X = 0, Y = Pos.Bottom(loggerView), Width = Dim.Auto(), Height = 1, Title = "Disconnect"
            };
            Add(stopButton);
            stopButton.Accepting += (sender, args) =>
            {
                _client?.Stop();
                args.Handled = true;
            };
        }
    }
}