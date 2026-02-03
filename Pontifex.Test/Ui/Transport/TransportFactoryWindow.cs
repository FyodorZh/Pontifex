using Scriba;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.UI;
using Terminal.UICommon;

namespace Pontifex.Test
{
    public sealed class TransportFactoryWindow : SmartWindow
    {
        private readonly TransportFactory _factory;
        private readonly TextField _urlField;
        private string _api = "";
        
        private readonly CheckBox _directTransport;
        private readonly CheckBox _tcpTransport;
        //private readonly CheckBox _udpTransport;
        
        private readonly CheckBox _zipProtocol;
        private readonly CheckBox _loggerProtocol;
        private readonly CheckBox _monitorProtocol;
        private readonly CheckBox _reconectableProtocol;
        //private readonly CheckBox _reliableProtocol;

        private readonly TextField _tcpUdpAddressField;
        private readonly TextField _tcpUdpPortField;
        
        private CheckBox _silentApi = null!;
        private CheckBox _bruteForceApi = null!;
        private CheckBox _bigApi = null!;
        
        
        public TransportFactoryWindow(TransportFactory factory)
            :base(false)
        {
            _factory = factory;
            
            Title = "Transport Factory";
            X = Pos.Center();
            Y = Pos.Center();
            Width = 55;
            Height = 17;
            
            var urlLabel = new Label() { Title = "Url: ", X = 0, Y = 0, Width = Dim.Auto(), Height = 1 };
            Add(urlLabel);

            _urlField = new TextField() { X = Pos.Right(urlLabel), Y = 0, Width = Dim.Fill(), Height = 1 };
            Add(_urlField);

            RadioButtonGroup transportGroup = new(
                _directTransport = new CheckBox() // direct
                {
                    RadioStyle = true,
                    Title = "direct",
                    X = 0, Y = 0, Width = Dim.Auto(), Height = 1, CheckedState = CheckState.Checked
                },
                _tcpTransport = new CheckBox() // tcp
                {
                    RadioStyle = true,
                    Title = "tcp",
                    X = 0, Y = 1, Width = Dim.Auto(), Height = 1
                })
            {
                Title = "Transports", X = 0, Y = Pos.Bottom(_urlField), Width = Dim.Fill(), Height = 4
            };
            transportGroup.CheckBoxActivated += _ => OnCheckedStateChanged();
            
            var addressLabel = new Label() { Title = "Address: ", X = 10, Y = Pos.Top(_tcpTransport), Width = Dim.Auto(), Height = 1 };
            _tcpUdpAddressField = new TextField() { X = Pos.Right(addressLabel), Y = Pos.Top(addressLabel), Width = 15, Height = 1 };
            _tcpUdpPortField = new TextField() { X = Pos.Right(_tcpUdpAddressField) + 1, Y = Pos.Top(addressLabel), Width = 6, Height = 1 };
            transportGroup.Add(addressLabel, _tcpUdpAddressField, _tcpUdpPortField);
            _tcpUdpAddressField.Text = "127.0.0.1";
            _tcpUdpPortField.Text = "12345";
            
            FrameView protocolsGroup = new FrameView()
            {
                Title = "Protocols", X = 0, Y = Pos.Bottom(transportGroup), Width = Dim.Fill(), Height = 4
            };
            Add(transportGroup, protocolsGroup);
            
            // Zip
            _zipProtocol = new CheckBox()
            {
                Title = "zip", X = 0, Y = 0, Width = Dim.Auto(), Height = 1
            };
            _zipProtocol.CheckedStateChanged += (_, _) => OnCheckedStateChanged();
            protocolsGroup.Add(_zipProtocol);
            
            // Monitor
            _monitorProtocol = new CheckBox()
            {
                Title = "monitor", X = Pos.Right(_zipProtocol)+ 1, Y = 0, Width = Dim.Auto(), Height = 1
            };
            _monitorProtocol.CheckedStateChanged += (_, _) => OnCheckedStateChanged();
            protocolsGroup.Add(_monitorProtocol);
            
            // Logger
            _loggerProtocol = new CheckBox()
            {
                Title = "log", X = Pos.Right(_monitorProtocol) + 1, Y = 0, Width = Dim.Auto(), Height = 1
            };
            _loggerProtocol.CheckedStateChanged += (_, _) => OnCheckedStateChanged();
            protocolsGroup.Add(_loggerProtocol);
            
            // Reconnectable
            _reconectableProtocol = new CheckBox()
            {
                Title = "reconnectable", X = 0, Y = 1, Width = Dim.Auto(), Height = 1
            };
            _reconectableProtocol.CheckedStateChanged += (_, _) => OnCheckedStateChanged();
            protocolsGroup.Add(_reconectableProtocol);
            
            
            var posY = SetupApiGroup(Pos.Bottom(protocolsGroup));

            var startServerAndClientBtn = new Button()
            {
                Text = "Start S&C", X = 0, Y = posY, Width = Dim.Auto(), Height = 1
            };
            var startServerBtn = new Button()
            {
                Title = "Start S", X = Pos.Right(startServerAndClientBtn) + 1, Y = posY, Width = Dim.Auto(), Height = 1
            };
            var startClientBtn = new Button()
            {
                Title = "Start C", X = Pos.Right(startServerBtn) + 1, Y = posY, Width = Dim.Auto(), Height = 1
            };
            var startApiBtn = new Button()
            {
                Title = "Start API", X = Pos.Right(startClientBtn) + 1, Y = posY, Width = Dim.Auto(), Height = 1
            };
            Add(startServerAndClientBtn, startServerBtn, startClientBtn, startApiBtn);

            startServerAndClientBtn.Accepting += (sender, args) => OnStart(args, true, true, true);
            startServerBtn.Accepting += (sender, args) => OnStart(args, true, false, true);
            startClientBtn.Accepting += (sender, args) => OnStart(args, false, true, true);
            startApiBtn.Accepting += (sender, args) => OnStart(args, true, true, true);

            void OnStart(CommandEventArgs args, bool startServer, bool startClient, bool startApi = false)
            {
                args.Handled = true;
                if (startServer) StartOneServer(startApi);
                if (startClient)
                {
                    if (startServer)
                    {
                        Task.Run(async () =>
                        {
                            await Task.Delay(1000);
                            Application.Invoke(() => StartOneClient(startApi));
                        });
                    }
                    else
                    {
                        StartOneClient(startApi);    
                    }
                }
            }

            OnCheckedStateChanged();
        }

        private Pos SetupApiGroup(Pos yPos)
        {
            RadioButtonGroup apiGroup = new(
                _silentApi = new CheckBox() // silent-api
                {
                    RadioStyle = true,
                    Title = "silent",
                    X = 0, Y = 0, Width = Dim.Auto(), Height = 1, CheckedState = CheckState.Checked
                },
                _bruteForceApi = new CheckBox() // brute-force-api
                {
                    RadioStyle = true,
                    Title = "brute-force",
                    X = 0, Y = 1, Width = Dim.Auto(), Height = 1
                },
                _bigApi = new CheckBox() // big-api
                {
                    RadioStyle = true,
                    Title = "big",
                    X = 0, Y = 2, Width = Dim.Auto(), Height = 1
                })
            {
                Title = "Api", X = 0, Y = yPos, Width = Dim.Fill(), Height = 5
            };
            apiGroup.CheckBoxActivated += _ => OnCheckedStateChanged();
            
            Add(apiGroup);

            return Pos.Bottom(apiGroup);
        }

        private void OnCheckedStateChanged()
        {
            string cmd = "";
            if (_directTransport.CheckedState == CheckState.Checked)
            {
                cmd = "direct|123";
            }
            else if (_tcpTransport.CheckedState == CheckState.Checked)
            {
                cmd = $"tcp|{_tcpUdpAddressField.Text}:{_tcpUdpPortField.Text}";
            }
            else
            {
                Log.e("No transport selected");
                return;
            }

            if (_reconectableProtocol.CheckedState == CheckState.Checked)
                cmd = "reconnectable|2000:" + cmd;
            if (_zipProtocol.CheckedState == CheckState.Checked) 
                cmd = "zip|9:" + cmd;
            if (_monitorProtocol.CheckedState == CheckState.Checked) 
                cmd = "monitor|" + cmd;
            if (_loggerProtocol.CheckedState == CheckState.Checked) 
                cmd = "log|" + cmd;
            
            _urlField.Text = cmd;

            if (_silentApi.CheckedState == CheckState.Checked)
                _api = "silent";
            if (_bruteForceApi.CheckedState == CheckState.Checked)
                _api = "brute";
            if (_bigApi.CheckedState == CheckState.Checked)
                _api = "big";
        }

        private void StartOneServer(bool startApi)
        {
            SuperView!.Add(new ServerWindow(_factory, _urlField.Text, startApi ? _api : null));
        }

        private void StartOneClient(bool startApi)
        {
            SuperView!.Add(new ClientWindow(_factory, _urlField.Text, startApi ? _api : null));
        }
    }
}