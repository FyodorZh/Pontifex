using System;
using Actuarius.Concurrent;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack
{
    public static class IRawAckServerHandler_Ext
    {
        public static IRawReliableAckServerHandler Test(this IRawReliableAckServerHandler core, Action<string> onFail)
        {
#if DEBUG
            return new TestWrapper(core, onFail);
#else
            return core;
#endif
        }

        public static IRawReliableAckServerHandler GetSafe(this IRawReliableAckServerHandler core, Action<Exception> onFail)
        {
            return new SafeWrapper(core, onFail);
        }

        private class SafeWrapper : IRawReliableAckServerHandler
        {
            private readonly IRawReliableAckServerHandler _handler;
            private readonly Action<Exception> _onException;

            public SafeWrapper(IRawReliableAckServerHandler handler, Action<Exception> onException)
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

            public void FillAckResponse(UnionDataList ackData)
            {
                _handler.FillAckResponse(ackData);
            }

            public void OnConnected(IRawReliableAckServerSideEndpoint endPoint)
            {
                _handler.OnConnected(endPoint);
            }
        }

        private class TestWrapper : InvariantChecker<TestWrapper.HandlerState>, IRawReliableAckServerHandler
        {
            public enum HandlerState
            {
                Constructed,
                Connected,
                Disconnected
            }

            private readonly IRawReliableAckServerHandler _core;

            private int _receiveDepth = 0;

            public TestWrapper(IRawReliableAckServerHandler core, Action<string> onFail)
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

            void IRawAckServerHandler.FillAckResponse(UnionDataList ackData)
            {
                _core.FillAckResponse(ackData);
            }

            void IRawReliableAckServerHandler.OnConnected(IRawReliableAckServerSideEndpoint endPoint)
            {
                ChangeState(HandlerState.Constructed, HandlerState.Connected);
                _core.OnConnected(endPoint);
            }

            void IRawAckBaseHandler.OnDisconnected(StopReason reason)
            {
                ChangeState(HandlerState.Connected, HandlerState.Disconnected);
                _core.OnDisconnected(reason);
            }

            void IRawAckBaseHandler.OnReceived(UnionDataList receivedBuffer)
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