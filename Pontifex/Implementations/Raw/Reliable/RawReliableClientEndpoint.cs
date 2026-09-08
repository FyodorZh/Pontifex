using System;
using System.Threading.Tasks;
using Actuarius.Concurrent;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    public abstract class RawReliableClientEndpoint : RawReliableEndpoint
    {
        private readonly PingOptions? _pingOptions;
        
        private readonly AtomicBox<DateTime> _lastPongReceiveTime = new ();

        protected RawReliableClientEndpoint(IRawReliableTransport owner, IEndPoint remoteEndPoint, IRawReliableClientHandler handler, 
            int messageMaxByteSize, long bufferCapacityBytes,
            PingOptions? pingOptions)
            : base(owner, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes)
        {
            if (pingOptions != null &&
                (pingOptions.Value.PingPeriod <= TimeSpan.Zero ||
                 pingOptions.Value.PingPeriod * 2 < pingOptions.Value.DisconnectTimeout))
            {
                throw new InvalidOperationException();
            }

            _pingOptions = pingOptions;
        }

        protected abstract bool BeginConnection();

        protected sealed override void OnStarted()
        {
            if (!BeginConnection())
            {
                StopNow(new TextFail(TransportName, "Failed to begin connection"));
            }
        }

        protected override void OnConnected()
        {
            _lastPongReceiveTime.Value = DateTime.Now;
            if (_pingOptions != null)
            {
                Task.Run(async () =>
                {
                    DateTime lastPingSentTime = new();
                    while (IsConnected)
                    {
                        DateTime now = DateTime.Now;
                        if (now - _lastPongReceiveTime.Value > _pingOptions.Value.DisconnectTimeout)
                        {
                            StopNow(new TimeOut(TransportName));
                            break;
                        }

                        if (now - lastPingSentTime > _pingOptions.Value.PingPeriod)
                        {
                            var buffer = Memory.SmallObjectsPool.Acquire<UnionDataList>().resource;
                            buffer.PutFirst((byte)MessageType.PingMessage);
                            var res = ScheduleToSend(buffer);
                            if (res != SendResult.Ok)
                            {
                                StopNow(new TextFail(TransportName, $"Ping failed with {res}"));
                                break;
                            }    
                        }
                        
                        await Task.Delay(1000);
                    }
                });
            }
        }

        protected override bool ProcessInboundMessage(MessageType messageType, UnionDataList message)
        {
            switch (messageType)
            {
                case MessageType.PongMessage:
                    _lastPongReceiveTime.Value = DateTime.Now;
                    return true;
                default:
                    return base.ProcessInboundMessage(messageType, message);
            }
        }

        protected struct PingOptions
        {
            public readonly TimeSpan DisconnectTimeout;
            public readonly TimeSpan PingPeriod;

            public PingOptions(TimeSpan disconnectTimeout, TimeSpan pingPeriod)
            {
                DisconnectTimeout = disconnectTimeout;
                PingPeriod = pingPeriod;
            }
        }
    }
}