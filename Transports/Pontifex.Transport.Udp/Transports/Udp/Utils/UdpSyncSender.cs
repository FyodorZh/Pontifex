using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Shared;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Controls;

namespace Pontifex.Transports.Udp
{
    internal class UdpSyncSender
    {
        private readonly Socket mSocket;
        private readonly EndPoint mRemoteEP;

        private readonly Action<Exception> mOnException;

        private readonly int mMaxMessageSize;
        private readonly MemoryChunkEncoder mChunkEncoder;

        private readonly ITrafficCollectorSink mTrafficCollector;

        public UdpSyncSender(Socket socket, EndPoint remoteEP, int maxMessageSize, Action<Exception> onException, ITrafficCollectorSink trafficCollector)
        {
            mSocket = socket;
            mRemoteEP = remoteEP;
            mOnException = onException;

            mMaxMessageSize = maxMessageSize;
            mChunkEncoder = new MemoryChunkEncoder(maxMessageSize + 2 + 4);

            mTrafficCollector = trafficCollector;
        }

        private SendResult SendChunk()
        {
            ByteArraySegment data = mChunkEncoder.ShowData();

            int count = mSocket.SendTo(data.ReadOnlyArray, data.Offset, data.Count, SocketFlags.None, mRemoteEP);
            mTrafficCollector.IncOutTraffic(count);
            mChunkEncoder.Clear();

            if (count != data.Count)
            {
                return SendResult.Error;
            }
            return SendResult.Ok;
        }

        public SendResult Send(IMacroOwner<Message> messages)
        {
            lock (mChunkEncoder)
            {
                try
                {
                    int count = messages.Count;
                    for (int i = 0; i < count; ++i)
                    {
                        var msg = messages[i];

                        if (msg.Data.Count > mMaxMessageSize)
                        {
                            return SendResult.MessageToBig;
                        }

                        if (!mChunkEncoder.TryPush(msg.Id, msg.Data))
                        {
                            var res = SendChunk();
                            if (res != SendResult.Ok)
                            {
                                return res;
                            }

                            if (!mChunkEncoder.TryPush(msg.Id, msg.Data))
                            {
                                return SendResult.Error;
                            }
                        }
                    }

                    if (!mChunkEncoder.IsEmpty)
                    {
                        var res = SendChunk();
                        if (res != SendResult.Ok)
                        {
                            return res;
                        }
                    }
                }
                catch (Exception ex)
                {
                    mOnException(ex);
                    mChunkEncoder.Clear();
                    return SendResult.Error;
                }
                finally
                {
                    messages.Release();
                }
            }
            return SendResult.Ok;
        }
    }
}
