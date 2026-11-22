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
        
        private readonly ICollectablePool _unionListPool;
        private readonly IPool<IMultiRefByteArray, int> _bytesPool;

        private const int mBufferSize = 1024 * 4;
        private readonly byte[] _buffer = new byte[mBufferSize];

        private readonly ILogger Log;
        private readonly ITrafficCollectorSink _trafficCollectorSink;
        private volatile bool _stopped;

        public UdpReceiver(Socket socket, IPEndPoint remoteEp, 
            Action<EndPoint, UnionDataList> onReceived,
            Action<SocketException> onFail,
            ICollectablePool unionListPool,
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
                UnionDataList? data = null;
                bool handedOff = false;

                try
                {
                    var count = _socket.ReceiveFrom(_buffer, SocketFlags.None, ref ep);
                    _trafficCollectorSink.IncInTraffic(count);

                    data = _unionListPool.Acquire<UnionDataList>();
                    var byteSource = new ByteSourceFromRealArray(_buffer, 0, count);
                    if (!data.Deserialize(ref byteSource, _bytesPool, count))
                    {
                        Log.w("Failed to read message from {0}", ep);
                        continue;
                    }

                    handedOff = true;

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
                    if (_stopped || IsRecoverable(ex.SocketErrorCode))
                    {
                        continue;
                    }

                    try
                    {
                        _onFail(ex);
                    }
                    catch (Exception inner)
                    {
                        Log.wtf(inner);
                    }

                    break;
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
                finally
                {
                    if (!handedOff)
                    {
                        data?.Release();
                    }
                }
            }
        }

        private static bool IsRecoverable(SocketError error)
        {
            switch (error)
            {
                case SocketError.ConnectionReset:
                case SocketError.ConnectionRefused:
                case SocketError.NetworkReset:
                case SocketError.HostUnreachable:
                case SocketError.NetworkUnreachable:
                case SocketError.MessageSize:
                case SocketError.Interrupted:
                case SocketError.WouldBlock:
                case SocketError.TryAgain:
                    return true;
                default:
                    return false;
            }
        }
    }
}
