using System;
using Shared;
using Transport.Abstractions.Endpoints.Client;
using Shared.Buffer;

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
        void OnConnected(IAckRawServerEndpoint endPoint, ByteArraySegment ackResponse);

        /// <summary>
        /// Вызывается при окончательном разрушении связи клиент-сервер.
        /// Если сработал OnConnected() то будет вызван: OnDisconnected(), далее OnStopped()
        /// Иначе только OnStopped()
        /// </summary>
        void OnStopped(StopReason reason);
    }

    public static class AckRawClientHandler
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

            private readonly IAckRawClientHandler mCore;

            private int mReceiveDepth = 0;

            public Wrapper(IAckRawClientHandler core, Action<string> onFail)
                : base(HandlerState.Constructed, onFail)
            {
                mCore = core;
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
                return string.Format("'{0}' - '{1}'", mCore, mCore.GetType());
            }

//            ~Wrapper()
//            {
//                if (State != HandlerState.Stopped)
//                {
//                    Fail();
//                }
//            }

            public byte[] GetAckData()
            {
                return mCore.GetAckData();
            }

            public void OnConnected(IAckRawServerEndpoint endPoint, ByteArraySegment ackResponse)
            {
                BeginCriticalSection(ref mReceiveDepth);

                ChangeState(HandlerState.Constructed, HandlerState.Connected);
                mCore.OnConnected(endPoint, ackResponse);

                EndCriticalSection(ref mReceiveDepth);
            }

            public void OnDisconnected(StopReason reason)
            {
                ChangeState(HandlerState.Connected, HandlerState.Disconnected);
                mCore.OnDisconnected(reason);
            }

            public void OnReceived(IMemoryBufferHolder receivedBuffer)
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

            public void OnStopped(StopReason reason)
            {
                var oldState = SetState(HandlerState.Stopped);
                if (oldState != HandlerState.Constructed && oldState != HandlerState.Disconnected)
                {
                    Fail(oldState.ToString());
                }
                mCore.OnStopped(reason);
            }
        }
    }
}
