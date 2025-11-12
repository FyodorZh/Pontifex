using System;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using Transport;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Flags;
using Transport.Abstractions.Servers;
using TransportAnalyzer.Hosts;

namespace TransportAnalyzer
{
    public partial class TrasnportConstructor : Form
    {
        private readonly ITransportFactory mServerFactory;
        private readonly ITransportFactory mClientFactory;

        public TrasnportConstructor(ITransportFactory serverFactory, ITransportFactory clientFactory)
        {
            InitializeComponent();
            mServerFactory = serverFactory;
            mClientFactory = clientFactory;
        }

        private void StartServer()
        {
            var uri = url.Text;

            Form form = null;

            var ackRawServer = mServerFactory.Construct(uri) as IAckRawServer;
            if (ackRawServer != null)
            {
                //form = new TransportServer(uri, ackRawServer);
                form = new AckRawProtocolServer(uri, ackRawServer);
            }

            if (form != null)
            {
                form.MdiParent = ParentForm;
                form.Show();
            }
        }

        private void StartClient()
        {
            for (int i = 0; i < numClients.Value; ++i)
            {
                Form form = null;

                var ackRawClient = mClientFactory.Construct(url.Text) as IAckRawClient;
                if (ackRawClient != null)
                {
                    form = new AckRawProtocolClient(url.Text, ackRawClient);
                }

                if (form != null)
                {
                    form.MdiParent = ParentForm;
                    form.Show();
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            StartServer();
            Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            StartClient();
            Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            StartServer();
            StartClient();
            Close();
        }

        private string TransportStack()
        {
            return (checkMonitor.Checked ? "monitor|" : "") + (checkReconnectable.Checked ? "reconnectable|20:" : "") + (checkCompress.Checked ? "zip|6:" : "");
        }

        private void directTab_Enter(object sender, EventArgs e)
        {
            url.Text = "direct|" + directServerName.Text;
            //url.Text = "monitor|reliable|reliable|10:memory|mem1,10,20,5";// TransportStack() + "rudp |" + udpIp.Text + ":" + udpPort.Text + "/10";
            //string fmt1 = "reliable|reliable|memory|{0},{1},{2},{3}";

            //url.Text = TransportStack() + "composer|" +
            //           //string.Format(fmt, directServerName.Text + "_1", 100, 100, 5) +
            //           "reliable|reliable|udp|127.0.0.1:23632" +
            //           "+" +
            //           string.Format(fmt1, directServerName.Text + "_2", 0, 0, 0);
        }

        private void tcpTab_Enter(object sender, EventArgs e)
        {
            url.Text = TransportStack() + "tcp|" + tcpIp.Text + ":" + tcpPort.Text + "/3";
        }

        private void redisTab_Enter(object sender, EventArgs e)
        {
            url.Text = TransportStack() + "redis|" + redisServerName.Text;
            url.Text = TransportStack() + "rudp|127.0.0.1:12346/3";
        }

        private void udpTab_Enter(object sender, EventArgs e)
        {
            url.Text = TransportStack() + "rudp2|" + udpIp.Text + ":" + udpPort.Text + "/3";
        }

        private void checkCompress_CheckedChanged(object sender, EventArgs e)
        {
            tabList.SelectedTab.Select();
        }

        private void numClients_ValueChanged(object sender, EventArgs e)
        {
            tabList.SelectedTab.Select();
        }

        private void TrasnportConstructor_Load(object sender, EventArgs e)
        {
            tcpIp.Items.Add("127.0.0.1");
            udpIp.Items.Add("127.0.0.1");

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.IsDnsEligible)
                    {
                        tcpIp.Items.Add(ip.Address.ToString());
                        udpIp.Items.Add(ip.Address.ToString());
                    }
                }
            }

            tcpIp.SelectedIndex = 0;
            udpIp.SelectedIndex = 0;
        }

        private void tcpIp_SelectedValueChanged(object sender, EventArgs e)
        {
            tcpTab_Enter(sender, e);
        }

        private void udpIp_SelectedValueChanged(object sender, EventArgs e)
        {
            udpTab_Enter(sender, e);
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            string uri = url.Text;

            var form = new TransportMultiClient(() => mClientFactory.Construct(uri) as IAckRawClient, (int)numClients.Value);
            form.MdiParent = ParentForm;
            form.Show();

            Close();
        }
    }
}
