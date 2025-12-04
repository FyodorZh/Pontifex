using System;
using Actuarius.ConcurrentPrimitives;
using Actuarius.Memory;
using Pontifex.Utils;
using Transport.Abstractions.Endpoints.Client;

namespace Transport.Abstractions.Handlers.Client
{
    public interface IAckRawClientHandler : IRawBaseHandler, IAckHandler
    {
        /// <summary>
        /// Логический коннект. Информирует бизнесс-логику, что всё в транспорте настроено, можно им пользоваться.
        /// Говорит об успешном подключении клиента к серверу (на стороне сервера создаётся сессия).
        /// </summary>
        /// <param name="endPoint"> EndoPoint до удалённого аганта </param>
        /// <param name="ackResponse"> Ответ сервера клиенту на его AckData </param>
        void OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse);

        /// <summary>
        /// Вызывается при окончательном разрушении связи клиент-сервер.
        /// Если сработал OnConnected() то будет вызван: OnDisconnected(), далее OnStopped()
        /// Иначе только OnStopped()
        /// </summary>
        void OnStopped(StopReason reason);
    }

    public static class IAckRawClientHandler_Ext
    {
        public static IAckRawClientHandler Test(this IAckRawClientHandler core, Action<string> onFail)
        {
#if DEBUG
            return new Wrapper(core, onFail);
#else
            return core;
#endif
        }

        private class Wrapper : InvariantChecker<Wrapper.HandlerState>, IAckRawClientHandler
        {
            public enum HandlerState
            {
                Constructed,
                Connected,
                Disconnected,
                Stopped
            }

            private readonly IAckRawClientHandler _core;

            private int _receiveDepth = 0;

            public Wrapper(IAckRawClientHandler core, Action<string> onFail)
                : base(HandlerState.Constructed, onFail)
            {
                _core = core;
            }

            protected override HandlerState ToState(int state)
            {
                return (HandlerState)state;
            }

            protected override int FromState(HandlerState state)
            {
                return (int)state;
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
//                if (State != HandlerState.Stopped)
//                {
//                    Fail();
//                }
//            }

            public void WriteAckData(UnionDataList ackData)
            {
                _core.WriteAckData(ackData);
            }

            public void OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
            {
                BeginCriticalSection(ref _receiveDepth);

                ChangeState(HandlerState.Constructed, HandlerState.Connected);
                _core.OnConnected(endPoint, ackResponse);

                EndCriticalSection(ref _receiveDepth);
            }

            public void OnDisconnected(StopReason reason)
            {
                ChangeState(HandlerState.Connected, HandlerState.Disconnected);
                _core.OnDisconnected(reason);
            }

            public void OnReceived(UnionDataList receivedBuffer)
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

            public void OnStopped(StopReason reason)
            {
                var oldState = SetState(HandlerState.Stopped);
                if (oldState != HandlerState.Constructed && oldState != HandlerState.Disconnected)
                {
                    Fail(oldState.ToString());
                }
                _core.OnStopped(reason);
            }
        }
    }
}
