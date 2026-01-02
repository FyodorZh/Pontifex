using System;
using Shared;
using Shared.Buffer;
using Transport.Abstractions;
using Transport.Abstractions.Handlers.Server;

namespace NewProtocol.Ping
{
    public class PingServer : INoAckUnreliableRRServerHandler
    {
        private readonly Transport.Abstractions.Servers.INoAckUnreliableRRServer mTransport;

        public event Action<IEndPoint, DateTime> OnMessageReceived;

        public bool IsStarted { get; private set; }

        public PingServer(Transport.Abstractions.Servers.INoAckUnreliableRRServer transport)
        {
            if (transport != null && transport.Init(this))
            {
                mTransport = transport;
            }
        }

        public bool Start(ILogger logger)
        {
            if (mTransport != null)
            {
                IsStarted = mTransport.Start((reason) => {
                    logger.w("Stop ping server with reason " + reason);
                    IsStarted = false;
                }, logger);
                return IsStarted;
            }

            return false;
        }

        public void Stop()
        {
            if (mTransport != null)
            {
                mTransport.Stop();
            }
        }

        void INoAckUnreliableRRServerHandler.OnRequest(IEndPoint client, Message message)
        {
            // (messageId, clientTime, buffer) => (messageId, remoteTime, buffer)

            IMemoryBufferHolder bufferHolder;
            if (ConcurrentUsageMemoryBufferPool.Instance.AllocateAndDeserialize(message.Data, out bufferHolder))
            {
                using (var bufferAccessor = bufferHolder.ExposeAccessorOnce())
                {
                    int messageId;
                    long clientTimeRaw;
                    if (bufferAccessor.Buffer.PopFirst().AsInt32(out messageId) && bufferAccessor.Buffer.PopFirst().AsInt64(out clientTimeRaw))
                    {
                        var onReceived = OnMessageReceived;
                        if (onReceived != null)
                        {
                            onReceived(client, DateTime.FromBinary(clientTimeRaw));
                        }

                        bufferAccessor.Buffer.PushInt64(DateTime.UtcNow.ToBinary());
                        bufferAccessor.Buffer.PushInt32(messageId);

                        mTransport.Send(client, new Message(message.Id, bufferAccessor.Acquire()));
                    }
                    else
                    {
                        Log.e("Invalid incoming message");
                    }
                }
            }
            message.Release();
        }
    }
}