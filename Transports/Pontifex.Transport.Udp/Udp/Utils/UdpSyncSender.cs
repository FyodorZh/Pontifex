using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.Abstractions.Controls;

namespace Pontifex.Transports.Udp
{
    internal class UdpSyncSender
    {
        private readonly Socket _socket;
        private readonly EndPoint _remoteEP;

        private readonly Action<Exception> _onException;
        private readonly ITrafficCollectorSink _trafficCollectorSink;

        private readonly int _maxMessageSize;

        private readonly object _locker = new();

        public UdpSyncSender(Socket socket, EndPoint remoteEP, int maxMessageSize, Action<Exception> onException,
            ITrafficCollectorSink trafficCollectorSink)
        {
            _socket = socket;
            _remoteEP = remoteEP;
            _onException = onException;
            _trafficCollectorSink = trafficCollectorSink;

            _maxMessageSize = maxMessageSize;
        }

        /// <summary>
        /// Sends a message to the remote endpoint.
        /// </summary>
        /// <param name="bytes">The message to send. Method owns the message and will release it after sending.</param>
        /// <returns>The result of the send operation.</returns>
        public SendResult Send(IMultiRefByteArray bytes)
        {
            if (bytes == null!)
            {
                return SendResult.InvalidMessage;
            }

            using var disposer = bytes.AsDisposable();
            
            if (bytes.Count > _maxMessageSize)
            {
                return SendResult.MessageToBig;
            }

            lock (_locker)
            {
                try
                {
                    int sentCount = _socket.SendTo(bytes.ReadOnlyArray, bytes.Offset, bytes.Count, SocketFlags.None, _remoteEP);
                    _trafficCollectorSink.IncOutTraffic(sentCount);
                    if (bytes.Count != sentCount)
                    {
                        return SendResult.Error;
                    }
                    return SendResult.Ok;
                }
                catch (Exception ex)
                {
                    _onException(ex);
                    return SendResult.Error;
                }
            }
        }
    }
}
