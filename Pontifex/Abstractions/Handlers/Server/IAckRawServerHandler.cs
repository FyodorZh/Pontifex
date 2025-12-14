using System;
using Actuarius.ConcurrentPrimitives;
using Actuarius.Memory;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Abstractions.Handlers.Server
{
    public interface IAckRawServerHandler : IRawBaseHandler
    {
        void GetAckResponse(UnionDataList ackData);

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

            public void GetAckResponse(UnionDataList ackData)
            {
                _handler.GetAckResponse(ackData);
            }

            public void OnConnected(IAckRawClientEndpoint endPoint)
            {
                _handler.OnConnected(endPoint);
            }

            public void Setup(IMemoryRental memory, ILogger logger)
            {
                _handler.Setup(memory, logger);
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

            private readonly IAckRawServerHandler _core;

            private int _receiveDepth = 0;

            public TestWrapper(IAckRawServerHandler core, Action<string> onFail)
                : base(HandlerState.Constructed, onFail)
            {
                _core = core;
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
                return $"'{_core}' - '{_core.GetType()}'";
            }

            public void Setup(IMemoryRental memory, ILogger logger)
            {
                _core.Setup(memory, logger);
            }

            //            ~Wrapper()
//            {
//                if (State != HandlerState.Disconnected)
//                {
//                    Fail();
//                }
//            }

            void IAckRawServerHandler.GetAckResponse(UnionDataList ackData)
            {
                _core.GetAckResponse(ackData);
            }

            void IAckRawServerHandler.OnConnected(IAckRawClientEndpoint endPoint)
            {
                ChangeState(HandlerState.Constructed, HandlerState.Connected);
                _core.OnConnected(endPoint);
            }

            void IRawBaseHandler.OnDisconnected(StopReason reason)
            {
                ChangeState(HandlerState.Connected, HandlerState.Disconnected);
                _core.OnDisconnected(reason);
            }

            void IRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
            {
                BeginCriticalSection(ref _receiveDepth);

                var curState = State;
                if (curState < HandlerState.Connected)
                {
                    Fail(curState.ToString());
                }

                _core.OnReceived(receivedBuffer);

                EndCriticalSection(ref _receiveDepth);
            }
        }
    }
}
