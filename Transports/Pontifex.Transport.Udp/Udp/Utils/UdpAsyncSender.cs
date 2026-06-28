using System;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Concurrent;
using Actuarius.Memory;
using Pontifex.Abstractions.Controls;
using Scriba;

namespace Pontifex.NoAckRaw.Udp
{
    internal class UdpAsyncSender
    {
        private readonly Socket _socket;
        private readonly int _maxMessageSize;
        private readonly Action<SocketException> _onSocketFailed;

        private readonly ConcurrentQueueValve<(EndPoint, IMultiRefByteArray)> _queueToSend;

        private readonly ILogger Log;
        private readonly ITrafficCollectorSink _trafficCollectorSink;

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        
        private readonly object _lock = new object();
        private bool _stopped;
        

        public UdpAsyncSender(Socket socket, int maxMessageSize, Action<SocketException> onSocketFailed, ILogger logger, 
            ITrafficCollectorSink trafficCollectorSink)
        {
            _queueToSend = new ConcurrentQueueValve<(EndPoint, IMultiRefByteArray)>(
                new LimitedConcurrentQueue<(EndPoint, IMultiRefByteArray)>(10000),
                pair => pair.Item2.Release());
            
            Log = logger;
            _trafficCollectorSink = trafficCollectorSink;
            _socket = socket;
            _maxMessageSize = maxMessageSize;
            _onSocketFailed = onSocketFailed;
            
            
            Thread thread = new Thread(DoWork, 1024 * 128)
            {
                IsBackground = true
            };
            thread.Start();
        }

        private void DoWork()
        {
            bool finalConsumption = false;
            while (true)
            {
                if (!finalConsumption)
                {
                    _resetEvent.WaitOne();
                }

                while (_queueToSend.TryPop(out var task))
                {
                    try
                    {
                        var (ep, bytes) = task;
                        var sent = _socket.SendTo(bytes.ReadOnlyArray, bytes.Offset, bytes.Count, SocketFlags.None, ep);
                        _trafficCollectorSink.IncOutTraffic(sent);
                        
                        if (sent != bytes.Count)
                        {
                            Log.e("Udp.Sender: Failed to send {0} bytes. Only {1} bytes were sent", bytes.Count, sent);
                        }
                    }
                    catch (ArgumentNullException ex)
                    {
                        Log.wtf(ex);
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        Log.wtf(ex);
                    }
                    catch (ObjectDisposedException)
                    {
                        Stop();
                    }
                    catch (InvalidOperationException ex)
                    {
                        Log.wtf(ex);
                    }
                    catch (SecurityException ex)
                    {
                        Log.wtf(ex);
                        Stop();
                    }
                    catch (SocketException ex)
                    {
                        Log.wtf("SocketException with code " + ex.ErrorCode, ex);
                        _onSocketFailed(ex);
                    }
                    finally
                    {
                        task.Item2.Release();
                    }
                }
                
                if (finalConsumption)
                {
                    break;
                }

                lock (_lock)
                {
                    if (_stopped)
                    {
                        _queueToSend.CloseValve();
                        finalConsumption = true;
                    }
                }
            }
        }

        public SendResult Send(EndPoint remoteEP, IMultiRefByteArray bytes)
        {
            if (bytes == null!)
            {
                return SendResult.InvalidMessage;
            }
            if (!bytes.IsValid || bytes.Count == 0 || bytes.Count > _maxMessageSize)
            {
                bytes.Release();
                return SendResult.InvalidMessage;
            }
            if (remoteEP == null!)
            {
                bytes.Release();
                return SendResult.InvalidAddress;
            }
            
            lock (_lock)
            {
                if (_stopped)
                {
                    bytes.Release();
                    return SendResult.NotConnected;
                }
            }
            
            switch (_queueToSend.EnqueueEx((remoteEP, bytes)))
            {
                case ValveEnqueueResult.Ok:
                    _resetEvent.Set();
                    return SendResult.Ok;
                case ValveEnqueueResult.Overflown:
                    return SendResult.BufferOverflow;
                case ValveEnqueueResult.Rejected:
                    return SendResult.NotConnected;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Stop(bool waitForQueueToEmpty = true)
        {
            lock (_lock)
            {
                if (!waitForQueueToEmpty)
                {
                    _queueToSend.CloseValve();
                }
                _stopped = true;
                _resetEvent.Set();
            }
        }
    }
}
