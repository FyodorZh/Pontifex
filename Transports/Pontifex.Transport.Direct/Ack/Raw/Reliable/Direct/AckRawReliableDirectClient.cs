using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Pontifex.Utils.FSM;
using Scriba;

namespace Pontifex.Ack.Raw.Reliable.Direct
{
    public class AckRawReliableDirectClient : AckRawReliableClient, IAckRawReliableClient, IClientDirectCtl
    {
        private enum State
        {
            Constructed,
            Connecting,
            Connected,
            Disconnected
        }

        private readonly StringEndPoint _serverEp;

        private readonly IConcurrentFSM<State> _state;

        private DirectTransport? _transport;
        
        private readonly AckRawReliableClientControl _transportControl;

        public override int MessageMaxByteSize => DirectInfo.MessageMaxByteSize;

        public AckRawReliableDirectClient(string serverName, ILogger logger, IMemoryRental memory)
            : base(DirectInfo.TransportName, logger, memory)
        {
            _serverEp = new StringEndPoint(serverName);

            var fsm = new RatchetFSM<State>((a, b) => ((int)a).CompareTo((int)b), State.Constructed);
            _state = new ConcurrentFSM<State>(fsm);
            _transportControl = new AckRawReliableClientControl(this);
        }

        protected override bool BeginConnect()
        {
            var handler = Handler;
            if (handler != null)
            {
                GuidEndPoint localEp = new GuidEndPoint(Guid.NewGuid());

                var transport = DirectTransportManager.Instance.NewTransport(
                    _serverEp,
                    localEp,
                    this);

                if (transport != null)
                {
                    _transport = transport;
                    _state.SetState(State.Connecting);
                    return true;
                }
            }
            return false;
        }

        protected override void OnReadyToConnect()
        {
            _transport!.FinishConnection();
        }

        protected override void DestroyTransport(StopReason reason)
        {
            _state.SetState(State.Disconnected);

            var transport = _transport;
            if (transport != null)
            {
                _transport = null;
                transport.Disconnect(reason);
            }
        }

        void IClientDirectCtl.GetAckData(UnionDataList ackData)
        {
            Handler?.FillAckData(ackData);
        }

        void IClientDirectCtl.GetTransportControls(List<IControl> dst, Predicate<IControl>? predicate)
        {
            if (predicate == null || predicate(_transportControl))
                dst.Add(_transportControl);
        }

        void IAnyDirectCtl.OnReceived(UnionDataList buffer)
        {
            using var bufferDisposer = buffer.AsDisposable();
            switch (_state.State)
            {
                case State.Connecting:
                {
                    if (buffer.TryPopFirst(out IMultiRefReadOnlyByteArray? ackOk) && ackOk.EqualByContent(DirectInfo.AckOKResponse))  
                    {
                        ackOk.Release();
                        buffer.AddRef();
                        _state.SetState(State.Connected, null, _ =>
                        {
                            ConnectionFinished(_transport!.ClientSide, buffer);
                        });
                    }
                    else
                    {
                        Log.w("Failed to parse ack response. Disconnecting...");
                        Stop(new AckRejected(Name));
                    }
                }
                    break;
                case State.Connected:
                    Handler?.OnReceived(buffer.Acquire());
                    break;
                default:
                    Fail(new TextFail("direct-client", "Wrong state"));
                    break;
            }
        }

        void IAnyDirectCtl.OnDisconnected(StopReason reason)
        {
            if (IsStarted)
            {
                Stop(reason);
            }
        }
    }
}