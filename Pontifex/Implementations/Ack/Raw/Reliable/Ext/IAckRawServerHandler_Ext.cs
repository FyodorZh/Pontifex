using System;
using Actuarius.Concurrent;
using Pontifex.Utils;

namespace Pontifex.Ack.Raw.Reliable
{
    public static class IAckRawServerHandler_Ext
    {
        public static IAckRawReliableServerHandler Test(this IAckRawReliableServerHandler core, Action<string> onFail)
        {
#if DEBUG
            return new TestWrapper(core, onFail);
#else
            return core;
#endif
        }

        public static IAckRawReliableServerHandler GetSafe(this IAckRawReliableServerHandler core, Action<Exception> onFail)
        {
            return new SafeWrapper(core, onFail);
        }

        private class SafeWrapper : IAckRawReliableServerHandler
        {
            private readonly IAckRawReliableServerHandler _handler;
            private readonly Action<Exception> _onException;

            public SafeWrapper(IAckRawReliableServerHandler handler, Action<Exception> onException)
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

            public void OnConnected(IAckRawReliableServerSideEndpoint endPoint)
            {
                _handler.OnConnected(endPoint);
            }
        }

        private class TestWrapper : InvariantChecker<TestWrapper.HandlerState>, IAckRawReliableServerHandler
        {
            public enum HandlerState
            {
                Constructed,
                Connected,
                Disconnected
            }

            private readonly IAckRawReliableServerHandler _core;

            private int _receiveDepth = 0;

            public TestWrapper(IAckRawReliableServerHandler core, Action<string> onFail)
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

            void IAckRawServerHandler.FillAckResponse(UnionDataList ackData)
            {
                _core.FillAckResponse(ackData);
            }

            void IAckRawReliableServerHandler.OnConnected(IAckRawReliableServerSideEndpoint endPoint)
            {
                ChangeState(HandlerState.Constructed, HandlerState.Connected);
                _core.OnConnected(endPoint);
            }

            void IAckRawBaseHandler.OnDisconnected(StopReason reason)
            {
                ChangeState(HandlerState.Connected, HandlerState.Disconnected);
                _core.OnDisconnected(reason);
            }

            void IAckRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
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