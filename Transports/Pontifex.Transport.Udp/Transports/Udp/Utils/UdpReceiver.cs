using System;
using System.Net;
using System.Net.Sockets;
using System.Security;
using Actuarius.PeriodicLogic;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Controls;
using Scriba;

namespace Pontifex.Transports.Udp
{
    internal class UdpReceiver : IPeriodicLogic
    {
        private readonly Socket mSocket;
        private readonly IPEndPoint mAnyRemoteEP;

        private readonly Action<EndPoint, IMacroOwner<Message>> mOnReceived;
        private readonly Action<SocketException> mOnFail;

        private readonly ITrafficCollectorSink mTrafficCollector;

        private const int mBufferSize = 1024 * 4;
        private readonly byte[] mBuffer = new byte[mBufferSize];

        private ILogicDriverCtl mDriverCtl;
        private ILogger Log = global::Log.StaticLogger;

        public event Action OnTick;

        /// <summary>
        ///
        /// </summary>
        /// <param name="socket"> Открытый, полностью настроенный UDP сокет </param>
        /// <param name="remoteEp"></param>
        /// <param name="onReceived"> Показывает пришедшие данные. Отдаёт их во владение. </param>
        /// <param name="onFail"> Информирует о проблемах </param>
        public UdpReceiver(Socket socket, IPEndPoint remoteEp, Action<EndPoint, IMacroOwner<Message>> onReceived, Action<SocketException> onFail,
            ITrafficCollectorSink trafficCollector)
        {
            mSocket = socket;
            mAnyRemoteEP = remoteEp;

            mOnReceived = onReceived;
            mOnFail = onFail;

            mTrafficCollector = trafficCollector;
        }

        bool IPeriodicLogic.LogicStarted(ILogicDriverCtl driver)
        {
            mDriverCtl = driver;
            Log = driver.Log;
            return true;
        }

        void IPeriodicLogic.LogicTick()
        {
            var onTick = OnTick;
            if (onTick != null)
            {
                onTick();
            }

            EndPoint ep = mAnyRemoteEP;
            int count = -1;

            while (true)
            {
                try
                {
                    if (mSocket.Available > 0)
                    {
                        count = mSocket.ReceiveFrom(mBuffer, SocketFlags.None, ref ep);
                        mTrafficCollector.IncInTraffic(count);
                    }
                    else
                    {
                        return;
                    }
                }
                catch (ArgumentNullException ex)
                {
                    Log.wtf(ex);
                    Stop();
                }
                catch (ObjectDisposedException)
                {
                    Stop();
                }
                catch (SecurityException ex)
                {
                    Log.wtf(ex);
                    Stop();
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.Interrupted)
                    {
                        mOnFail(ex);
                    }
                }

                if (ep == null)
                {
                    Log.e("Udp.Received {0} bytes from null EP", count);
                    return;
                }

                if (count > 0)
                {
                    try
                    {
                        MemoryChunkDecoder decoder = new MemoryChunkDecoder(mBuffer, count);
                        mOnReceived(ep, decoder.DecodeAll());
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                }
            }
        }

        void IPeriodicLogic.LogicStopped()
        {
        }

        public void Stop()
        {
            var driver = mDriverCtl;
            if (driver != null)
            {
                driver.Stop();
            }
        }
    }
}
