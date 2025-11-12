using System;
using System.Windows.Forms;
using Transport;
using Transport.Abstractions;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Controls;
using TransportAnalyzer.TestLogic;

namespace TransportAnalyzer
{
    public partial class TransportClient : Form
    {
        private readonly string mUrl;
        private readonly ITransport mClient;

        private IPingCollector mPingMonitor;
        private ITrafficCollector mTrafficMonitor;

        private readonly AckRawClientLogic mAckRawLogic;

        public TransportClient(string url, IAckRawClient client)
            : this(url, client as ITransport)
        {
            mAckRawLogic = new AckRawClientLogic();
            client.Init(mAckRawLogic);
            InitControllers();
        }

        private TransportClient(string url, ITransport client)
        {
            InitializeComponent();

            mUrl = url;
            mClient = client;
        }

        private void InitControllers()
        {
            mPingMonitor = mClient.TryGetControl<IPingCollector>();
            mTrafficMonitor = mClient.TryGetControl<ITrafficCollector>();
            foreach (var ctl in mClient.GetControls<IPingCollector>("monitor"))
            {
                mPingMonitor = ctl;
                break;
            }
            foreach (var ctl in mClient.GetControls<ITrafficCollector>("monitor"))
            {
                mTrafficMonitor = ctl;
                break;
            }

            Log(string.Format("Ping monitor from '{0}'", mPingMonitor != null ? mPingMonitor.Name : "null"));
            Log(string.Format("Traffic monitor from '{0}'", mTrafficMonitor != null ? mTrafficMonitor.Name : "null"));

            tabComposer.Hide();
        }

        private void OnStopped(StopReason reason)
        {
            Invoke((Action)(() => Log("Stopped " + reason)));
            mTrafficMonitor = null;
            mPingMonitor = null;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            {
                string logicText = mClient.ToString();
                if (mAckRawLogic != null)
                {
                    logicText = mAckRawLogic.ToString();
                }
                Text = mUrl + "   " + mClient + "   " + logicText;
            }


            if (mTrafficMonitor != null && mPingMonitor != null)
            {
                mPingMonitor.GetPing(out var minPing, out var maxPing, out var avgPing);
                Log($"Packets: In={mTrafficMonitor.InPacketsSpeed} / Out={mTrafficMonitor.OutPacketsSpeed} Speed: In={mTrafficMonitor.InSpeed} / Out={mTrafficMonitor.OutSpeed}  Total: In={mTrafficMonitor.InTraffic} / Out={mTrafficMonitor.OutTraffic}  Ping={minPing}/{maxPing}");
            }
        }

        private void TransportClient_FormClosed(object sender, FormClosedEventArgs e)
        {
            mClient.Stop();
        }

        private void Log(string text)
        {
            logText.Items.Insert(0, text);
        }

        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mClient.Stop();
        }

        private void TransportClient_Load(object sender, EventArgs e)
        {
            var logger = global::Log.LoggerInstance(new ListBoxLogConsumer(logText, 500));
            mAckRawLogic.Log = logger;
            if (mClient.Start(OnStopped, logger))
            {
                Log("Started");
            }
        }
    }
}
