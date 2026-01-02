using System;
using System.Text;
using Actuarius.ConcurrentPrimitives;
using Actuarius.PeriodicLogic;
using Shared;
using Transport;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers;
using Transport.Abstractions.Handlers.Client;
using Transport.Handlers;
using NewProtocol.Client;
using Pontifex.Utils;
using Pontifex.Utils.FSM;
using Shared.Concurrent;
using Transport.Abstractions;
using Transport.StopReasons;

namespace NewProtocol
{
    public abstract class AckRawClientProtocol : AnySideProtocol<IAckRawServerEndpoint>, IAckRawClientHandler
    {
        private readonly IAckRawClient mTransport;
        private readonly SynchronizedAckRawClientHandler mSynchronized;

        private readonly Intention mIntentionToStop = new Intention();
        private DateTime mTimeWhenStop = DateTime.MaxValue;

        private Protocol mProtocol;

        public enum State
        {
            Constructed,
            Connected,
            Disconnected
        }

        private readonly IConcurrentFSM<State> mState;

        public bool IsConnected
        {
            get { return mState.State == State.Connected; }
        }

        protected abstract void WriteAckData(UnionDataList ackData);

        protected abstract bool OnConnecting(bool protocolIsValid, ByteArraySegment ackResponse);

        protected virtual void OnConnected()
        {
        }

        protected virtual void OnDisconnected()
        {
        }

        protected AckRawClientProtocol(IAckRawClient transport, IModelsHashDB protocolModelHashes, IDateTimeProvider timeProvider, bool synchronize = true)
            : base(timeProvider)
        {
            var fsm = new RatchetFSM<State>((a, b) => ((int)a).CompareTo((int)b), State.Constructed);
            mState = new ConcurrentFSM<State>(fsm);

            ((IAnyProtocolCtl)this).SetProtocolHashes(protocolModelHashes);
            mTransport = transport;
            if (synchronize)
            {
                mSynchronized = new SynchronizedAckRawClientHandler(this,
                    () => this.Stop(new TextFail("sync-protocol", "Buffer overflow")));
                mTransport.Init(mSynchronized);
            }
            else
            {
                mTransport.Init(this);
            }
        }

        protected override void BindToProtocol(Protocol protocol)
        {
            mProtocol = protocol;
            base.BindToProtocol(protocol);
        }

        public void GracefulStop(DeltaTime dt)
        {
            if (dt.MilliSeconds <= 0 || mProtocol == null)
            {
                Stop();
            }
            else
            {
                if (mIntentionToStop.Set())
                {
                    mTimeWhenStop = DateTime.UtcNow.AddSeconds(dt.Seconds);
                    mProtocol.Disconnect.Send(new DisconnectMessage());
                    InvalidateProtocol();
                    SetState(State.Disconnected);
                }
            }
        }

        public sealed override bool IsServerMode
        {
            get { return false; }
        }

        protected sealed override bool TryStart()
        {
            Log.i("Starting ClientProtocol[{0}]. Hash = '{1}'", GetType().Name, ProtocolHash);
            return mTransport.Start(r => { Log.i("Stopped ClientProtocol[{0}] underlying transport with reason '{@stopReason}'", r.Print()); }, Log);
        }

        protected sealed override void OnDisconnected(StopReason reason)
        {
            SetState(State.Disconnected);
        }

        protected override void OnTick(DateTime now)
        {
            if (mTimeWhenStop <= now)
            {
                Stop(new UserIntention("client-protocol", "graceful stop"));
            }

            if (mSynchronized != null)
            {
                mSynchronized.Service();
            }

            base.OnTick(now);
        }

        #region IAckRawClientHandler

        void IAckHandler.WriteAckData(UnionDataList ackData)
        {
            WriteAckData(ackData);
        }

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, ByteArraySegment ackResponse)
        {
            ByteArraySegment hash = new ByteArraySegment(Encoding.UTF8.GetBytes(ProtocolHash));

            ByteArraySegment serverHash;
            var response = AckUtils.GetPrefix(ackResponse, hash.Count, out serverHash);
            if (!response.IsValid)
            {
                endPoint.Disconnect(new Transport.StopReasons.TextFail("protocol", "Failed to get server hash from ack response"));
                SetState(State.Disconnected);
                return;
            }

            bool isHashOk = serverHash.EqualByContent(hash);
            if (!isHashOk)
            {
                string serverHashString;
                try
                {
                    serverHashString = Encoding.UTF8.GetString(serverHash.ReadOnlyArray, serverHash.Offset, serverHash.Count);
                }
                catch
                {
                    serverHashString = "<invalid>";
                }
                Log.e("Wrong protocol hash. Expected '{0}', received '{1}'", ProtocolHash, serverHashString);
            }

            if (OnConnecting(isHashOk, response))
            {
                OnConnected(endPoint);
                SetState(State.Connected);
            }
            else
            {
                endPoint.Disconnect(new Transport.StopReasons.TextFail("protocol", "Connection rejected by client logic"));
                SetState(State.Disconnected);
            }
        }

        void IAckRawClientHandler.OnStopped(StopReason reason)
        {
            Log.i("ClientProtocol.Stop: {@stopReason}", reason.Print());
            Stop();
        }

        #endregion

        protected TControl TryGetTransportControl<TControl>()
            where TControl : class, IControl
        {
            return mTransport.TryGetControl<TControl>();
        }

        private void SetState(State newState)
        {
            switch (newState)
            {
                case State.Disconnected:
                    mState.SetState(newState, (oldState, nextState) =>
                    {
                        if (oldState == State.Connected)
                        {
                            OnDisconnected();
                        }
                        return true;
                    });
                    break;
                case State.Connected:
                    mState.SetState(newState, (oldState, nextState) =>
                    {
                        if (oldState == State.Constructed)
                        {
                            OnConnected();
                        }
                        return true;
                    });
                    break;
                default:
                    mState.SetState(newState);
                    break;
            }
        }
    }
}
