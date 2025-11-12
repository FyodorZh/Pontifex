using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Transport.Abstractions;
using Transport.Abstractions.Servers;
using TransportAnalyzer.TestLogic;

namespace TransportAnalyzer.Hosts
{
    public partial class AckRawProtocolServer : Form
    {
        private readonly string mUrl;
        private readonly IAckRawServer mTransport;
        private readonly AckRawTestFactory mFactory;

        public AckRawProtocolServer(string url, IAckRawServer server)
        {
            InitializeComponent();
            mUrl = url;
            mTransport = server;
            mFactory = new AckRawTestFactory(server, OnConnected, OnDisconnected);
        }

        private void AckRawProtocolServer_Load(object sender, EventArgs e)
        {
            var logger = global::Log.LoggerInstance(new ListBoxLogConsumer(logBox, 500));

            mFactory.Start(logger, Shared.DeltaTime.FromMiliseconds(25), 2);

            Text = mUrl;
        }

        private void AckRawProtocolServer_FormClosing(object sender, FormClosingEventArgs e)
        {
            mFactory.Dispose();
        }

        private void Log(string text)
        {
            logBox.Items.Insert(0, text);
        }

        private void OnConnected(AckRawTestServer server)
        {
            Invoke((Action)(() => clientList.Items.Add(server)));
        }

        private void OnDisconnected(AckRawTestServer server)
        {
            Invoke((Action)(() => clientList.Items.Remove(server)));
        }

        private void clientList_MouseDown(object sender, MouseEventArgs e)
        {
            int index = clientList.IndexFromPoint(e.Location);
            clientList.SelectedIndex = index;
        }

        private void stopProtocolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (clientList.SelectedItem is AckRawTestServer server)
            {
                server.Stop();
            }
        }

        private void stopTransportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (clientList.SelectedItem is AckRawTestServer server)
            {
                server.StopTransport();
            }
        }
    }
}
