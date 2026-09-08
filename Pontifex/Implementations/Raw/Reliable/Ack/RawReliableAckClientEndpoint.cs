using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Pontifex.Utils.FSM;

namespace Pontifex.Raw.Reliable.Ack
{
    public abstract class RawReliableAckClientEndpoint : RawReliableClientEndpoint
    {
        private readonly IRawReliableAckClientHandler _handler;

        private enum State
        {
            /// <summary>
            /// Before the begining of a low-level connection
            /// </summary>
            Constructed,

            /// <summary>
            /// Low-level connection in probress
            /// </summary>
            LowLevelConnecting,

            /// <summary>
            /// Ack request sent but ack response is not received
            /// </summary>
            AckRequestSent,

            /// <summary>
            /// Ack response received but is not approved
            /// </summary>
            AckResponseReceived,

            /// <summary>
            /// Ack response received and approved
            /// </summary>
            Connected,

            /// <summary>
            /// After sending of the disconnection message
            /// </summary>
            Disconnecting,

            /// <summary>
            /// Low-level connection destroyed
            /// </summary>
            Disconnected
        }

        private readonly IFSM<State> _stage;

        protected RawReliableAckClientEndpoint(IRawReliableTransport owner,
            IEndPoint remoteEndPoint, IRawReliableAckClientHandler handler,
            int messageMaxByteSize, long bufferCapacityBytes,
            PingOptions? pingOptions)
            : base(owner, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes, pingOptions)
        {
            _handler = handler;

            FSM<State, int> fsm = new FSM<State, int>(State.Constructed, s => (int)s, OnStateChanged_);
            fsm.AddTransitions(State.Constructed, new[] { State.LowLevelConnecting, State.Disconnected });
            fsm.AddTransitions(State.LowLevelConnecting, new[] { State.AckRequestSent, State.Disconnected });
            fsm.AddTransitions(State.AckRequestSent, new[] { State.AckResponseReceived, State.Disconnected });
            fsm.AddTransitions(State.AckResponseReceived, new[] { State.Connected, State.Disconnected });
            fsm.AddTransitions(State.Connected, new[] { State.Disconnecting, State.Disconnected });

            _stage = new SerializedFSM<State>(fsm);
        }

        private void OnStateChanged_(State from, State to)
        {
            Log.d($"RawReliableAckClient: State change: {from} -> {to}");
        }

        protected sealed override bool BeginConnection()
        {
            try
            {
                return BeginLowLevelConnection();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return false;
            }
        }

        protected abstract bool BeginLowLevelConnection();

        protected void CompleteLowLevelConnection()
        {
            if (_stage.SetState(State.AckRequestSent))
            {
                UnionDataList ackRequest = Memory.SmallObjectsPool.GetPool<UnionDataList>().Acquire();
                using var disposer = ackRequest.AsDisposable();
                try
                {
                    lock (_handlerLock)
                    {
                        _handler.FillAckData(ackRequest);
                    }
                    ackRequest.PutFirst((byte)MessageType.AckRequestMessage);

                    if (ScheduleToSend(ackRequest.Acquire()) != SendResult.Ok)
                    {
                        StopNow(new TextFail(TransportName, "Failed to send ACK"));
                    }
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    StopNow(new TextFail(TransportName, "FillAckData throw an exception"));
                }
            }
            else
            {
                StopNow(new TextFail(TransportName, "Failed to complete low level connection for RawReliableAck client"));
            }
        }

        private volatile UnionDataList? _ackResponseMessage;

        protected override bool ProcessInboundMessage(MessageType messageType, UnionDataList message)
        {
            switch (messageType)
            {
                case MessageType.AckResponseMessage:
                    if (Interlocked.CompareExchange(ref _ackResponseMessage, message, null) == null)
                    {
                        MarkConnected();
                    }
                    return true;
                default:
                    return base.ProcessInboundMessage(messageType, message);
            }
        }

        protected override void OnConnected()
        {
            base.OnConnected();
            lock (_handlerLock)
            {
                _handler.OnConnected(this, _ackResponseMessage!);
            }
        }
    }
}