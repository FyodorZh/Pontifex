using Actuarius.Memory;
using Pontifex.UI;
using Scriba;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.UI;
using Pontifex.Abstractions.Clients;
using Pontifex.Api;
using TransportAnalyzer.TestLogic;

namespace Pontifex.Test
{
    public sealed class ClientWindow : SmartWindow
    {
        private readonly IAckRawClient? _transport;
        private readonly ILogger _logger;
        
        public ClientWindow(TransportFactory factory, string url, string? startApi)
        {
            X = Pos.AnchorEnd();
            Y = 1;
            Title = "Client: " + url;
            this.SetResizable(190, 20);

            ControlsPanel controlsPanel = new()
            {
                X = 0, Y = 0, Width = 30, Height = Dim.Fill(1), BorderStyle = LineStyle.Rounded
            };
            Add(controlsPanel);
            
            LoggerView loggerView = new()
            {
                X = Pos.Right(controlsPanel), Y = 0, Width = Dim.Fill(), Height = Dim.Fill(1),
                BorderStyle = LineStyle.Rounded
            };
            Add(loggerView);

            _logger = new Logger([loggerView]);  
            
            
            _transport = factory.ConstructClient(url, _logger, MemoryRental.Shared);
            if (_transport == null)
            {
                _logger.e("Failed to construct transport");
                return;
            }

            if (startApi == null)
            {
                var clientLogic = new AckRawClientLogic(_transport.Memory, _transport.Log, 1);
                clientLogic.Connected += endpoint => { controlsPanel.SetEndpoint(endpoint); };
                clientLogic.Disconnected += _ => { controlsPanel.SetEndpoint(null); };

                if (!_transport.Init(clientLogic))
                {
                    _logger.e("Failed to init transport");
                    return;
                }
            }
            else
            {
                var api = ApiFactory.Construct(startApi, true, _transport.Memory, _transport.Log);
                ClientSideApi apiLogic = new ClientSideApi(api, _transport.Memory, _transport.Log);
                apiLogic.Connected += endpoint => { controlsPanel.SetEndpoint(endpoint); };
                apiLogic.Disconnected += _ => { controlsPanel.SetEndpoint(null); };
                if (!_transport.Init(apiLogic))
                {
                    _logger.e("Failed to init transport");
                    return;
                }
            }

            bool started = _transport.Start(stopReason =>
            {
                _logger.e("OnStopped: " + stopReason);
            });
            _logger.i($"Starting: {(started ? "OK" : "FAILED")}");

            Button stopButton = new()
            {
                X = 0, Y = Pos.Bottom(loggerView), Width = Dim.Auto(), Height = 1, Title = "Transport.Stop"
            };
            Add(stopButton);
            stopButton.Accepting += (_, args) =>
            {
                _transport?.Stop();
                args.Handled = true;
            };
        }
    }
}