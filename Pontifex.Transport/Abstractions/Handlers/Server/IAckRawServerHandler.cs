using System;
using Actuarius.ConcurrentPrimitives;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Utils;
using Transport.Abstractions.Endpoints.Server;

namespace Transport.Abstractions.Handlers.Server
{
    public interface IAckRawServerHandler : IRawBaseHandler
    {
        byte[] GetAckResponse();

        /// <summary>
        /// Логический коннект. Информирует бизнесс-логику, что всё в транспорте настроено, можно им пользоваться.
        /// Говорит об успешном подключении клиента к серверу (на стороне сервера создаётся сессия).
        /// </summary>
        /// <param name="endPoint"> EndoPoint до удалённого аганта </param>
        void OnConnected(IAckRawClientEndpoint endPoint);
    }

    public static class IAckRawServerHandler_Ext
    {
        public static IAckRawServerHandler Test(this IAckRawServerHandler core, Action<string> onFail)
        {
#if DEBUG
            return new TestWrapper(core, onFail);
#else
            return core;
#endif
        }

        public static IAckRawServerHandler GetSafe(this IAckRawServerHandler core, Action<Exception> onFail)
        {
            return new SafeWrapper(core, onFail);
        }

        private class SafeWrapper : IAckRawServerHandler
        {
            private readonly IAckRawServerHandler _handler;
            private readonly Action<Exception> _onException;

            public SafeWrapper(IAckRawServerHandler handler, Action<Exception> onException)
            {
                _handler = handler;
                _onException = onException;
            }

            public void OnDisconnected(StopReason reason)
            {
                try
                {
                    _handler.OnDisconnected(reason);
                }
                catch (Exception e)
                {
                    _onException(e);
                }
            }

            public void OnReceived(UnionDataList receivedBuffer)
            {
                try
                {
                    _handler.OnReceived(receivedBuffer);
                }
                catch (Exception e)
                {
                    _onException(e);
                }
            }

            public byte[] GetAckResponse()
            {
                return _handler.GetAckResponse();
            }

            public void OnConnected(IAckRawClientEndpoint endPoint)
            {
                _handler.OnConnected(endPoint);
            }
        }

        private class TestWrapper : InvariantChecker<TestWrapper.HandlerState>, IAckRawServerHandler
        {
            public enum HandlerState
            {
                Constructed,
                Connected,
                Disconnected
            }

            private readonly IAckRawServerHandler mCore;

            private int mReceiveDepth = 0;

            public TestWrapper(IAckRawServerHandler core, Action<string> onFail)
                : base(HandlerState.Constructed, onFail)
            {
                mCore = core;
            }

            protected override int FromState(HandlerState state)
            {
                return (int)state;
            }

            protected override HandlerState ToState(int state)
            {
                return (HandlerState)state;
            }

            public override string ToString()
            {
                return string.Format("'{0}' - '{1}'", mCore, mCore.GetType());
            }

//            ~Wrapper()
//            {
//                if (State != HandlerState.Disconnected)
//                {
//                    Fail();
//                }
//            }

            byte[] IAckRawServerHandler.GetAckResponse()
            {
                return mCore.GetAckResponse();
            }

            void IAckRawServerHandler.OnConnected(IAckRawClientEndpoint endPoint)
            {
                ChangeState(HandlerState.Constructed, HandlerState.Connected);
                mCore.OnConnected(endPoint);
            }

            void IRawBaseHandler.OnDisconnected(StopReason reason)
            {
                ChangeState(HandlerState.Connected, HandlerState.Disconnected);
                mCore.OnDisconnected(reason);
            }

            void IRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
            {
                BeginCriticalSection(ref mReceiveDepth);

                var curState = State;
                if (curState < HandlerState.Connected)
                {
                    Fail(curState.ToString());
                }

                mCore.OnReceived(receivedBuffer);

                EndCriticalSection(ref mReceiveDepth);
            }
        }
    }
}
