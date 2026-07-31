using System;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Controls;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.NetSockets
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
        private volatile bool _stopped;

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

            socket.ReceiveTimeout = 1000; // macOS: Close() doesn't unblock ReceiveFrom, so poll _stopped periodically

            Thread thread = new Thread(DoWork, 1024 * 128)
            {
                IsBackground = true
            };
            thread.Start();
        }

        public void Stop()
        {
            _stopped = true;
            _socket.Close();
        }

        private void DoWork()
        {
            EndPoint ep = _anyRemoteEP;

            while (!_stopped)
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
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.Interrupted)
                    {
                        _onFail(ex);
                    }
                    break;
                }
            }
        }
    }
}
