using System;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Abstractions.Controls;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Transports.Udp
{
    internal class UdpReceiver
    {
        private readonly Socket _socket;
        private readonly IPEndPoint _anyRemoteEP;

        private readonly Action<EndPoint, UnionDataList> _onReceived;
        private readonly Action<SocketException> _onFail;
        
        private readonly IPool<UnionDataList> _unionListPool;
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
        /// <param name="unionListPool"> Пул для хранения объединённых данных </param>
        /// <param name="bytesPool"> Пул байтов для временного хранения данных </param>
        /// <param name="logger"> Логгер для записи сообщений </param>
        /// <param name="trafficCollectorSink"> Сборщик трафика для мониторинга входящего трафика </param>
        public UdpReceiver(Socket socket, IPEndPoint remoteEp, 
            Action<EndPoint, UnionDataList> onReceived,
            Action<SocketException> onFail,
            IPool<UnionDataList> unionListPool,
            IPool<IMultiRefByteArray, int> bytesPool,
            ILogger logger,
            ITrafficCollectorSink trafficCollectorSink)
        {
            _socket = socket;
            _anyRemoteEP = remoteEp;

            _onReceived = onReceived;
            _onFail = onFail;
            _unionListPool = unionListPool;

            Log = logger;
            _trafficCollectorSink = trafficCollectorSink;
            _bytesPool = bytesPool;

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

                    var data = _unionListPool.Acquire();
                    using var disposer = data.AsDisposable();
                    
                    var byteSource = new ByteSourceFromRealArray(_buffer, 0, count);
                    if (!data.Deserialize(ref byteSource, _bytesPool))
                    {
                        Log.w("Failed to read message from {0}", ep);
                        continue;
                    }
                    
                    try 
                    {
                        _onReceived(ep, data);
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
