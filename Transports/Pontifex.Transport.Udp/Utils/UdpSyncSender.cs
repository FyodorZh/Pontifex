using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.Abstractions.Controls;
using Pontifex.Utils;

namespace Pontifex.Transports.Udp
{
    internal class UdpSyncSender
    {
        private readonly Socket _socket;
        private readonly EndPoint _remoteEP;

        private readonly Action<Exception> _onException;
        private readonly ITrafficCollectorSink _trafficCollectorSink;

        private readonly int _maxMessageSize;
        
        private readonly IPool<IMultiRefByteArray, int> _bytesPool;

        private readonly object _locker = new();

        public UdpSyncSender(Socket socket, EndPoint remoteEP, int maxMessageSize,
            IPool<IMultiRefByteArray, int> bytesPool,
            Action<Exception> onException,
            ITrafficCollectorSink trafficCollectorSink)
        {
            _socket = socket;
            _remoteEP = remoteEP;
            _onException = onException;
            _trafficCollectorSink = trafficCollectorSink;
            _bytesPool = bytesPool;

            _maxMessageSize = maxMessageSize;
        }

        /// <summary>
        /// Sends a message to the remote endpoint.
        /// </summary>
        /// <param name="dataToSend">The message to send. Method owns the message and will release it after sending.</param>
        /// <returns>The result of the send operation.</returns>
        public SendResult Send(UnionDataList dataToSend)
        {
            if (dataToSend == null!)
            {
                return SendResult.InvalidMessage;
            }

            using var disposer = dataToSend.AsDisposable();
            
            if (dataToSend.GetDataSize() > _maxMessageSize)
            {
                return SendResult.MessageToBig;
            }

            if (!dataToSend.Serialize(_bytesPool, out var serializedData))
            {
                return SendResult.InvalidMessage;
            }
            using var serializedDisposer = serializedData.AsDisposable();
            
            lock (_locker)
            {
                try
                {
                    int sentCount = _socket.SendTo(serializedData.ReadOnlyArray, serializedData.Offset, serializedData.Count, SocketFlags.None, _remoteEP);
                    _trafficCollectorSink.IncOutTraffic(sentCount);
                    if (serializedData.Count != sentCount)
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
