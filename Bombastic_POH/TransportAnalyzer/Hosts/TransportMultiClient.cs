using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Transport.Abstractions.Clients;
using TransportAnalyzer.TestLogic;

namespace TransportAnalyzer.Hosts
{
    public partial class TransportMultiClient : Form
    {
        private readonly Func<IAckRawClient> mClientFactory;
        private readonly int mClientsCount;
        private ILogger mLogger;

        public TransportMultiClient(Func<IAckRawClient> clientFactory, int clientsCount)
        {
            mClientFactory = clientFactory;
            mClientsCount = clientsCount;
            InitializeComponent();
        }

        private void TransportMulitClient_Load(object sender, EventArgs e)
        {
            mLogger = global::Log.LoggerInstance(new ListBoxLogConsumer(log, 500));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            System.Threading.Thread thread = new System.Threading.Thread(() =>
            {
                for (int i = 0; i < mClientsCount; ++i)
                {
                    mLogger.i("------> " + i);
                    var client = mClientFactory.Invoke();
                    client.Init(new AckRawClientLogic(10, -1));
                    client.Start(r => { }, mLogger);
                    System.Threading.Thread.Sleep(10);
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }
    }
}
