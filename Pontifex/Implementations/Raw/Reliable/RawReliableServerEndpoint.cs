using System;
using Actuarius.Concurrent;
using Operarius;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    public abstract class RawReliableServerEndpoint : RawReliableEndpoint
    {
        private readonly ILogicRunner<IPeriodicLogicDriverCtl> _runner;
        private readonly AtomicBox<DateTime> _lastInboundMessage = new();

        private readonly PeriodicDelegate _periodicLogic;
        
        protected RawReliableServerEndpoint(IRawReliableTransport owner, IEndPoint remoteEndPoint, IRawReliableServerHandler handler, 
            int messageMaxByteSize, long bufferCapacityBytes,
            TimeSpan disconnectTimeout,
            ILogicRunner<IPeriodicLogicDriverCtl> runner)
            : base(owner, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes)
        {
            _runner = runner;
            _periodicLogic = new PeriodicDelegate(_ =>
            {
                if (DateTime.Now - _lastInboundMessage.Value > disconnectTimeout)
                {
                    StopNow(new TimeOut(TransportName));
                }
            });
        }

        protected override void OnConnected()
        {
            _lastInboundMessage.Value = DateTime.Now;
            _runner.Start(_periodicLogic);
        }

        protected override void OnDisconnected()
        {
            _periodicLogic.Stop();
        }

        protected override bool ProcessInboundMessage(MessageType messageType, UnionDataList message)
        {
            _lastInboundMessage.Value = DateTime.Now;
            switch (messageType)
            {
                case MessageType.PingMessage:
                {
                    var buffer = Memory.SmallObjectsPool.Acquire<UnionDataList>().resource;
                    buffer.PutFirst((byte)MessageType.PongMessage);
                    var res = ScheduleToSend(buffer);
                    if (res != SendResult.Ok)
                    {
                        StopNow(new TextFail(TransportName, "Failed to send Pong message"));
                        return false;
                    }

                    return true;
                }
                default:
                    return base.ProcessInboundMessage(messageType, message);
            }
        }
    }
}