using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Shared.Utils;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Controls;
using TransportAnalyzer.TestLogic;

namespace TransportAnalyzer.Hosts
{
    public partial class AckRawProtocolClient : Form
    {
        private readonly string mUrl;
        private readonly IAckRawClient mTransport;
        private readonly AckRawTestClient mClient;
        private PeriodicLogicManualDriver mDriver;

        private IPingCollector mPingMonitor;
        private ITrafficCollector mTrafficMonitor;


        public AckRawProtocolClient(string url, IAckRawClient client)
        {
            InitializeComponent();
            mUrl = url;
            mTransport = client;
            mClient = new AckRawTestClient(client, 15);
        }

        private void AckRawProtocolClient_Load(object sender, EventArgs e)
        {
            var logger = global::Log.LoggerInstance(new ListBoxLogConsumer(logBox, 500));

            mClient.Started += () =>
            {
                Log("Started!");
            };
            mClient.Stopped += (r) =>
            {
                logger.i("Stopped! {@StopReason}", r.Print());
                timer1000.Enabled = false;
            };

            mDriver = new PeriodicLogicManualDriver(Shared.DeltaTime.FromMiliseconds(50));
            mDriver.Start(mClient, logger);

            mPingMonitor = mTransport.TryGetControl<IPingCollector>();
            mTrafficMonitor = mTransport.TryGetControl<ITrafficCollector>();
            foreach (var ctl in mTransport.GetControls<IPingCollector>("monitor"))
            {
                mPingMonitor = ctl;
                break;
            }
            foreach (var ctl in mTransport.GetControls<ITrafficCollector>("monitor"))
            {
                mTrafficMonitor = ctl;
                break;
            }

            Log(string.Format("Ping monitor from '{0}'", mPingMonitor != null ? mPingMonitor.Name : "null"));
            Log(string.Format("Traffic monitor from '{0}'", mTrafficMonitor != null ? mTrafficMonitor.Name : "null"));
        }

        private void AckRawProtocolClient_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (mDriver != null)
            {
                mDriver.StopAndTick();
                mDriver = null;
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (mDriver != null)
            {
                mDriver.Tick(DateTime.UtcNow);
            }
        }

        private void timer1000_Tick(object sender, EventArgs e)
        {
            Text = mUrl + " " + mClient.ToString();
            if (mTrafficMonitor != null && mPingMonitor != null)
            {
                mPingMonitor.GetPing(out var minPing, out var maxPing, out var avgPing);
                Log($"Packets: In={mTrafficMonitor.InPacketsSpeed} / Out={mTrafficMonitor.OutPacketsSpeed} Speed: In={mTrafficMonitor.InSpeed} / Out={mTrafficMonitor.OutSpeed}  Total: In={mTrafficMonitor.InTraffic} / Out={mTrafficMonitor.OutTraffic}  Ping={minPing}/{maxPing}");
            }
        }

        private void Log(string text)
        {
            try
            {
                logBox.Items.Insert(0, text);
            }
            catch (Exception e)
            {
            }
        }

        private void protocolGracefulStopBtn_Click(object sender, EventArgs e)
        {
            mClient.GracefulStop(Shared.DeltaTime.OneSec);
        }

        private void protocolStopBtn_Click(object sender, EventArgs e)
        {
            mClient.Stop();
        }

        private void transportStopBtn_Click(object sender, EventArgs e)
        {
            mTransport.Stop(Transport.StopReason.UserIntention);
        }
    }
}
