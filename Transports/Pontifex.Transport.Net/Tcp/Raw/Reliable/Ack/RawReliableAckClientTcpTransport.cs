using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Operarius;
using Pontifex.NetSockets;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Tcp
{
    public class RawReliableAckClientTcpTransport : RawReliableAckClientTransport
    {
        
        private readonly IPEndPoint mRemoteEP;
        private readonly IpEndPoint mManagedRemoteEP;
        private readonly TimeSpan mDisconnectTimeout;
        
        private Socket? mSocket;
        
        public override int MessageMaxByteSize => TcpInfo.DefaultMessageMaxSize;
        
        public RawReliableAckClientTcpTransport(IPAddress ipAddress, int port, TimeSpan disconnectTimeout, ILogger logger, IMemoryRental memory) 
            : base(TcpInfo.TransportName, logger, memory)
        {
            mRemoteEP = new IPEndPoint(ipAddress, port);
            mManagedRemoteEP = new IpEndPoint(mRemoteEP);
            mDisconnectTimeout = disconnectTimeout;
        }

        protected override bool BeginConnect()
        {
            try
            {
                mSocket = new Socket(mRemoteEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                mSocket.ReceiveTimeout = (int)mDisconnectTimeout.TotalMilliseconds;
                mSocket.SendTimeout = (int)mDisconnectTimeout.TotalMilliseconds;
                mSocket.NoDelay = true;
                /*
                
                TcpClientEndpoint ep = new TcpClientEndpoint(this,
                    mManagedRemoteEP, msgToSend => { },
                    )

                UnionDataList ackData = Memory.CollectablePool.Acquire<UnionDataList>();
                (Handler ?? throw new Exception("Handler is null")).FillAckData(ackData);
                ackData.PutFirst(TcpInfo.AckRequest);

                mSocket.BeginConnect(mRemoteEP, ConnectCallback, ackData);

                mLastMessageReceiveTime.Time = DateTime.UtcNow;
                try
                {
                    TimeSpan keepAlivePeriod = TimeSpan.FromMilliseconds(1000);

                    mKeepAliver = new KeepAliver(this, Memory);

                    var driver = new SingleJobLogicDriver<IPeriodicLogicDriverCtl>(
                        new ThreadBasedPeriodicMultiLogicDriver(NowDateTimeProvider.Instance, keepAlivePeriod));
                    if (driver.Start(mKeepAliver) != LogicStartResult.Success)
                    {
                        throw new Exception("Couldn't start mKeepAliverSharedLogicRunner");
                    }
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    return false;
                }
*/
                return true;
            }
            catch (Exception ex1)
            {
                Log.wtf(ex1);
                return false;
            }
        }

        protected override void OnReadyToConnect()
        {
            throw new System.NotImplementedException();
        }

        protected override void DestroyTransport(StopReason reason)
        {
            throw new System.NotImplementedException();
        }
    }
}