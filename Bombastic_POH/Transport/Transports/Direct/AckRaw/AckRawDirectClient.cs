using System;
using Shared;
using Shared.Buffer;
using Shared.FSM;
using Transport.Endpoints;
using Transport.StopReasons;
using Transport.Transports.Core;

namespace Transport.Transports.Direct
{
    public class AckRawDirectClient : AckRawClient, IClientDirectCtl
    {
        private enum State
        {
            Constructed,
            Connecting,
            Connected,
            Disconnected
        }

        private readonly StringEndPoint mServerEp;

        private readonly IConcurrentFSM<State> mState;

        private DirectTransport mTransport;

        public override int MessageMaxByteSize
        {
            get
            {
                return DirectInfo.MessageMaxByteSize;
            }
        }

        public AckRawDirectClient(string serverName)
            : base(DirectInfo.TransportName)
        {
            mServerEp = new StringEndPoint(serverName);

            var fsm = new RatchetFSM<State>((a, b) => ((int)a).CompareTo((int)b), State.Constructed);
            mState = new ConcurrentFSM<State>(fsm);
        }

        protected override bool BeginConnect()
        {
            var handler = Handler;
            if (handler != null)
            {
                GuidEndPoint localEp = new GuidEndPoint(Guid.NewGuid());

                var transport = DirectTransportManager.Instance.NewTransport(
                    mServerEp,
                    localEp,
                    (IClientDirectCtl)this);

                if (transport != null)
                {
                    mTransport = transport;
                    mState.SetState(State.Connecting);
                    return true;
                }
            }
            return false;
        }

        protected override void OnReadyToConnect()
        {
            mTransport.FinishConnection();
        }

        protected override void DestroyTransport(StopReason reason)
        {
            mState.SetState(State.Disconnected);

            var transport = mTransport;
            if (transport != null)
            {
                mTransport = null;
                transport.Disconnect(reason);
            }
        }

        void IClientDirectCtl.GetAckData(UnionDataList ackData)
        {
            var handler = Handler;
            if (handler != null)
            {
                handler.WriteAckData(ackData);
            }
        }

        void IAnyDirectCtl.OnReceived(IMemoryBufferHolder buffer)
        {
            using (var bufferAccessor = buffer.ExposeAccessorOnce())
            {
                switch (mState.State)
                {
                    case State.Connecting:
                        {
                            ByteArraySegment ackResponse;
                            bufferAccessor.Buffer.PopFirst().AsArray(out ackResponse);
                            ackResponse = AckUtils.CheckPrefix(ackResponse, DirectInfo.AckOKResponse);
                            if (ackResponse.IsValid)
                            {
                                mState.SetState(State.Connected);
                                ConnectionFinished(mTransport.ClientSide, ackResponse);
                            }
                            else
                            {
                                Log.w("Failed to parse ack response. Disconnecting...");
                                Stop(new StopReasons.AckRejected(Type));
                            }
                        }
                        break;
                    case State.Connected:
                        var handler = Handler;
                        if (handler != null)
                        {
                            handler.OnReceived(bufferAccessor.Acquire());
                        }
                        break;
                    default:
                        Fail(new TextFail("direct-client", "Wrong state"));
                        break;
                }
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