using Shared;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers.Client;

namespace Transport.Transports.Core
{
    public abstract class AckRawClient : AbstractTransport, IAckRawClient
    {
        private enum State
        {
            Constructed,
            Initialized,
            Connecting,
            Connected,
            Disconnected
        }

        private State mState = State.Constructed;

        private readonly string mTypeName;

        private IAckRawClientHandler mHandler;

        public sealed override string Type
        {
            get { return mTypeName; }
        }

        protected AckRawClient(string typeName)
        {
            mTypeName = typeName;
        }

        protected virtual IAckRawClientHandler SetupHandler(IAckRawClientHandler handler)
        {
            return handler;
        }

        /// <summary>
        /// Информирует наследника о необходимости начать подключаться к серверу
        /// </summary>
        /// <returns> TRUE если удалось запустить процесс подключения. </returns>
        protected abstract bool BeginConnect();

        /// <summary>
        /// Транспорт переходит в состояние когла можно завершить процесс коннекта вызовом метода
        /// </summary>
        protected abstract void OnReadyToConnect();

        protected abstract void DestroyTransport(StopReason reason);

        public bool IsInited
        {
            get
            {
                lock (mLocker)
                {
                    return mState >= State.Initialized;
                }
            }
        }

        public bool IsConnected
        {
            get
            {
                lock (mLocker)
                {
                    return mState == State.Connected;
                }
            }
        }

        protected IAckRawClientHandler Handler
        {
            get { return mHandler; }
        }

        public bool Init(IAckRawClientHandler handler)
        {
            lock (mLocker)
            {
                if (IsValid)
                {
                    if (handler != null)
                    {
                        handler = handler.Test(text => Log.e(text));

                        var processedHandler = SetupHandler(handler);
                        if (processedHandler != null)
                        {
                            if (!IsStarted && mState == State.Constructed)
                            {
                                mHandler = processedHandler;
                                mState = State.Initialized;
                                return true;
                            }
                            Fail("Init", "Wrong transport state (state={0}, started={1})", mState, IsStarted);
                        }
                        else
                        {
                            Fail("Init", "failed to setup handler");
                        }
                    }
                    else
                    {
                        Fail("Init", "handler is null");
                    }
                }
                return false;
            }
        }

        public abstract int MessageMaxByteSize { get; }

        protected sealed override bool TryStart()
        {
            if (mState == State.Initialized)
            {
                if (BeginConnect())
                {
                    if (mState == State.Initialized)
                    {
                        mState = State.Connecting;
                    }
                    return true;
                }
                Fail("TryStart", "Transport implementation failed to begin connection process");
                return false;
            }
            Fail("TryStart", "Transport is not initialized");
            return false;
        }

        protected sealed override void OnStarted()
        {
            OnReadyToConnect();
        }

        protected sealed override void OnStopped(StopReason reason)
        {
            if (mState == State.Connecting || mState == State.Connected)
            {
                if (mHandler != null)
                {
                    if (mState == State.Connected)
                    {
                        mHandler.OnDisconnected(reason);
                    }
                    mHandler.OnStopped(reason);
                }

                mState = State.Disconnected;
                DestroyTransport(reason);
            }
        }

        protected void ConnectionFailed()
        {
            ConnectionFinished(false, null, new ByteArraySegment());
        }

        protected void ConnectionFinished(IAckRawServerEndpoint endPoint, ByteArraySegment ackResponse)
        {
            ConnectionFinished(true, endPoint, ackResponse);
        }

        private void ConnectionFinished(bool success, IAckRawServerEndpoint endPoint, ByteArraySegment ackResponse)
        {
            lock (mLocker)
            {
                if (mState == State.Connecting)
                {
                    if (success)
                    {
                        mState = State.Connected;
                        mHandler.OnConnected(endPoint, ackResponse);
                    }
                    else
                    {
                        Fail("ConnectionFinished", "Connection failed");
                    }
                }
                else
                {
                    Stop(new StopReasons.TextFail(Type, "Transport has wrong state '{0}' instead of 'Connecting'", mState));
                }
            }
        }

        protected void Disconnected()
        {
            Stop();
        }
    }
}
