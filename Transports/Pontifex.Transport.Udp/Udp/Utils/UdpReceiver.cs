using System;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Abstractions.Controls;
using Scriba;

namespace Pontifex.NoAckRaw.Udp
{
    internal class UdpReceiver
    {
        private readonly Socket _socket;
        private readonly IPEndPoint _anyRemoteEP;

        private readonly Action<EndPoint, IMultiRefByteArray> _onReceived;
        private readonly Action<SocketException> _onFail;
        private readonly IPool<IMultiRefByteArray, int> _bytesPool;

        private const int mBufferSize = 1024 * 4;
        private readonly byte[] _buffer = new byte[mBufferSize];

        private readonly ILogger Log;
        private readonly ITrafficCollectorSink _trafficCollectorSink;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="socket"> Открытый, полностью настроенный UDP сокет </param>
        /// <param name="remoteEp"></param>
        /// <param name="onReceived"> Показывает пришедшие данные. Отдаёт их во владение. </param>
        /// <param name="onFail"> Информирует о проблемах </param>
        /// <param name="bytesPool"> Пул байтов для декодирования сообщений </param>
        /// <param name="logger"> Логгер для записи сообщений </param>
        /// <param name="trafficCollectorSink"> Сборщик трафика для мониторинга входящего трафика </param>
        public UdpReceiver(Socket socket, IPEndPoint remoteEp, 
            Action<EndPoint, IMultiRefByteArray> onReceived,
            Action<SocketException> onFail,
            IPool<IMultiRefByteArray, int> bytesPool,
            ILogger logger,
            ITrafficCollectorSink trafficCollectorSink)
        {
            _socket = socket;
            _anyRemoteEP = remoteEp;

            _onReceived = onReceived;
            _onFail = onFail;
            _bytesPool = bytesPool;

            Log = logger;
            _trafficCollectorSink = trafficCollectorSink;

            Thread thread = new Thread(DoWork, 1024 * 128)
            {
                IsBackground = true
            };
            thread.Start();
        }

        public void Stop()
        {
            _socket.Close();
        }

        private void DoWork()
        {
            EndPoint ep = _anyRemoteEP;

            while (_socket.Connected)
            {
                try
                {
                    var count = _socket.ReceiveFrom(_buffer, SocketFlags.None, ref ep);
                    _trafficCollectorSink.IncInTraffic(count);
                    
                    var bytes = _bytesPool.Acquire(count);
                    bytes.CopyFrom(_buffer, 0, 0, count);
                    try 
                    {
                        _onReceived(ep, bytes);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                }
                catch (ArgumentNullException ex)
                {
                    Log.wtf(ex);
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SecurityException ex)
                {
                    Log.wtf(ex);
                    break;
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.Interrupted)
                    {
                        _onFail(ex);
                    }
                }
            }
        }
    }
}
