using System;
using System.Collections.Concurrent;
using System.Windows.Forms;
using Transport.Abstractions;
using Transport.Abstractions.Servers;
using TransportAnalyzer.TestLogic;

namespace TransportAnalyzer
{
    public partial class TransportServer : Form
    {
        private readonly ITransport mServer;

        private readonly AckRawServerLogic mAckRawLogic;

        private readonly ConcurrentQueue<string> mLog = new ConcurrentQueue<string>();

        public TransportServer(string url, IAckRawServer server)
            : this(url, server as ITransport)
        {
            mAckRawLogic = new AckRawServerLogic();

            mAckRawLogic.ClientAdded += (client) =>
                {
                    Invoke((Action)(() => clientList.Items.Add(client)));
                };
            mAckRawLogic.ClientRemoved += (client) =>
                {
                    Invoke((Action)(() => clientList.Items.Remove(client)));
                };

            server.Init(mAckRawLogic);
            //InitControllers();
        }

        private TransportServer(string url, ITransport server)
        {
            InitializeComponent();
            Text = url;

            mServer = server;
        }

        private void TransportServer_FormClosed(object sender, FormClosedEventArgs e)
        {
            mServer.Stop();
        }

        private void TransportServer_Load(object sender, System.EventArgs e)
        {
            var logger = Log.LoggerInstance(new ListBoxLogConsumer(log, 500));
            mAckRawLogic.Log = logger;
            mServer.Start(r => mLog.Enqueue("Stopped " + r), logger);
        }

        private void disconnectToolStripMenuItem_Click(object sender, System.EventArgs e)
        {
            AckRawServerLogic.IClientHandler handler = clientList.SelectedItem as AckRawServerLogic.IClientHandler;
            if (handler != null)
            {
                handler.Disconnect(Transport.StopReason.UserIntention);
            }
        }

        private void clientList_MouseDown(object sender, MouseEventArgs e)
        {
            int index = clientList.IndexFromPoint(e.Location);
            clientList.SelectedIndex = index;
        }
    }
}
