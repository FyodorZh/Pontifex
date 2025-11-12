using System;
using System.Windows.Forms;
using Transport.Protocols.Monitoring.AckRaw;
using Transport.Protocols.Reconnectable.AckReliableRaw;
using Transport.Protocols.Reliable.AckRaw;
using Transport.Protocols.Zip.AckRaw;
using Transport.Transports.Direct;
using Transport.Transports.RUdp;
using Transport.Transports.Tcp;
using Transport.Transports.Udp;

namespace TransportAnalyzer
{
    public partial class TransportAnalyzer : Form
    {
        private readonly Transport.TransportFactory mClientFactory = new Transport.TransportFactory();
        private readonly Transport.TransportFactory mServerFactory = new Transport.TransportFactory();

        public TransportAnalyzer()
        {
            InitializeComponent();

            mClientFactory.Register(new AckRawDirectClientProducer());
            mServerFactory.Register(new AckRawDirectServerProducer());

            mClientFactory.Register(new AckRawMonitoringClientProducer());
            mServerFactory.Register(new AckRawMonitoringServerProducer());

            mClientFactory.Register(new AckRawZipClientProducer());
            mServerFactory.Register(new AckRawZipServerProducer());

            mClientFactory.Register(new AckRawTcpClientProducer());
            mServerFactory.Register(new AckRawTcpServerProducer());

            mClientFactory.Register(new NoAckRRUdpClientProducer());
            mServerFactory.Register(new NoAckRRUdpServerProducer());

            mClientFactory.Register(new NoAckUnreliableRawUdpClientProducer());
            mServerFactory.Register(new NoAckUnreliableRawUdpServerProducer());

            mClientFactory.Register(new AckRawRUdpClientProducer());
            mServerFactory.Register(new AckRawRUdpServerProducer());

            mClientFactory.Register(new AckReliableRawUdpProducer());
            mServerFactory.Register(new AckReliableRawUdpProducer());

            mClientFactory.Register(new AckRawReliableClientProducerNew());
            mServerFactory.Register(new AckRawReliableServerProducerNew());

            mClientFactory.Register(new AckRawReconnectableClientProducer());
            mServerFactory.Register(new AckRawReconnectableServerProducer());

            mClientFactory.Register(new AckRawLoggerClientProducer());
            mServerFactory.Register(new AckRawLoggerServerProducer());
        }

        private void startDirectTransportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = new TrasnportConstructor(mServerFactory, mClientFactory);
            form.MdiParent = this;
            form.Show();
        }

        private void gCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.GC.Collect(2, GCCollectionMode.Forced);
        }
    }
}
