using System;
using Actuarius.Concurrent;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack
{
    public static class IRawAckClientHandler_Ext
    {
        public static IRawReliableAckClientHandler Test(this IRawReliableAckClientHandler core, Action<string> onFail)
        {
#if DEBUG
            return new Wrapper(core, onFail);
#else
            return core;
#endif
        }

        private class Wrapper : InvariantChecker<Wrapper.HandlerState>, IRawReliableAckClientHandler
        {
            public enum HandlerState
            {
                Constructed,
                Connected,
                Disconnected,
                Stopped
            }

            private readonly IRawReliableAckClientHandler _core;

            private int _receiveDepth = 0;

            public Wrapper(IRawReliableAckClientHandler core, Action<string> onFail)
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

            public void FillAckData(UnionDataList ackData)
            {
                _core.FillAckData(ackData);
            }

            public void OnConnected(IRawReliableEndpoint endPoint, UnionDataList ackResponse)
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
                _core.OnStopped(reason: reason);
            }
        }
    }
}