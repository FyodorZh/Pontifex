using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;
using Pontifex.Utils.FSM;
using Transport.Abstractions.Clients;
using Transport.Endpoints;
using Transport.StopReasons;
using Transport.Transports.Core;

namespace Transport.Transports.Direct
{
    public class AckRawDirectClient : AckRawClient, IAckReliableRawClient, IClientDirectCtl
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

        public override int MessageMaxByteSize => DirectInfo.MessageMaxByteSize;

        public AckRawDirectClient(string serverName)
            : base(DirectInfo.TransportName)
        {
            _serverEp = new StringEndPoint(serverName);

            var fsm = new RatchetFSM<State>((a, b) => ((int)a).CompareTo((int)b), State.Constructed);
            _state = new ConcurrentFSM<State>(fsm);
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
                    (IClientDirectCtl)this);

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
            Handler?.WriteAckData(ackData);
        }

        void IAnyDirectCtl.OnReceived(UnionDataList buffer)
        {
            try
            {
                switch (_state.State)
                {
                    case State.Connecting:
                    {
                        if (buffer.PopFirstAsArray(out var ackOk) && ackOk.EqualByContent(DirectInfo.AckOKResponse))  
                        {
                            _state.SetState(State.Connected);
                            ConnectionFinished(_transport!.ClientSide, buffer.Acquire());
                        }
                        else
                        {
                            Log.w("Failed to parse ack response. Disconnecting...");
                            Stop(new StopReasons.AckRejected(Type));
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
            finally
            {
                buffer.Release();
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