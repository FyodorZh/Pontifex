using System;
using Actuarius.Concurrent;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Shared extensions for reliable server handlers.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Exception isolation</b>: <c>GetSafe</c> guards <c>OnDisconnected</c>,
    /// <c>OnReceived</c> and <c>OnStopped</c>. It deliberately forwards <c>OnConnected</c> unguarded;
    /// the reliable specification requires a throw from that callback to reach the endpoint.</description></item>
    /// </list>
    /// </remarks>
    public static class IRawReliableServerHandler_Ext
    {
        /// <summary>
        /// Wraps the handler with a debug-only invariant checker.
        /// </summary>
        /// <param name="core">The handler to wrap.</param>
        /// <param name="onFail">Receives a description of a violated invariant.</param>
        /// <returns>The wrapped handler in Debug builds, otherwise <paramref name="core"/>.</returns>
        /// <summary>
        /// Wraps the handler with a debug-only invariant checker.
        /// </summary>
        /// <param name="core">The handler to wrap.</param>
        /// <param name="onFail">Receives a description of a violated invariant.</param>
        /// <returns>The wrapped handler in Debug builds, otherwise <paramref name="core"/>.</returns>
        public static IRawReliableServerHandler Test(this IRawReliableServerHandler core, Action<string> onFail)
        {
#if DEBUG
            return new TestServerHandler(core, onFail);
#else
            return core;
#endif
        }

        /// <summary>
        /// Wraps the handler so that exceptions thrown by its callbacks are passed to
        /// <paramref name="onFail"/> instead of escaping.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><c>OnDisconnected</c>, <c>OnReceived</c> and <c>OnStopped</c> are guarded.</description></item>
        /// <item><description><c>OnConnected</c> is forwarded unguarded: the specification requires that
        /// its exception reaches the endpoint, which then stops the session with <c>ExceptionFail</c> and
        /// delivers no <c>OnDisconnected</c>.</description></item>
        /// </list>
        /// </remarks>
        /// <param name="core">The handler to wrap.</param>
        /// <param name="onFail">Receives every exception thrown by a guarded callback.</param>
        /// <returns>The guard wrapper around <paramref name="core"/>.</returns>
        /// <summary>
        /// Wraps the handler so that exceptions thrown by its callbacks are passed to
        /// <paramref name="onFail"/> instead of escaping.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><c>OnDisconnected</c>, <c>OnReceived</c> and <c>OnStopped</c> are guarded.</description></item>
        /// <item><description><c>OnConnected</c> is forwarded unguarded; see the specification, which
        /// requires the exception to reach the endpoint.</description></item>
        /// </list>
        /// </remarks>
        /// <param name="core">The handler to wrap.</param>
        /// <param name="onFail">Receives every exception thrown by a guarded callback.</param>
        /// <returns>The guard wrapper around <paramref name="core"/>.</returns>
        public static IRawReliableServerHandler GetSafe(this IRawReliableServerHandler core, Action<Exception> onFail)
        {
            return new SafeServerHandler(core, onFail);
        }

        private sealed class SafeServerHandler : IRawReliableServerHandler
        {
            private readonly IRawReliableServerHandler _handler;
            private readonly Action<Exception> _onException;

            public SafeServerHandler(IRawReliableServerHandler handler, Action<Exception> onException)
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

            public void OnStopped(StopReason reason)
            {
                try
                {
                    _handler.OnStopped(reason);
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

            /// <summary>
            /// Forwards a logical connect without a guard.
            /// </summary>
            /// <remarks>
            /// A throw from this callback must reach the endpoint: the specification requires the endpoint
            /// to stop the session with <c>ExceptionFail</c> and to deliver no <c>OnDisconnected</c>.
            /// Guarding it here would swallow that throw.
            /// </remarks>
            public void OnConnected(IRawReliableEndpoint endPoint)
            {
                _handler.OnConnected(endPoint);
            }
        }

        private sealed class TestServerHandler : InvariantChecker<TestServerHandler.HandlerState>, IRawReliableServerHandler
        {
            public enum HandlerState
            {
                Constructed,
                Connected,
                Disconnected,
                Stopped
            }

            private readonly IRawReliableServerHandler _core;

            private int _receiveDepth = 0;

            public TestServerHandler(IRawReliableServerHandler core, Action<string> onFail)
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

            void IRawReliableServerHandler.OnConnected(IRawReliableEndpoint endPoint)
            {
                ChangeState(HandlerState.Constructed, HandlerState.Connected);
                _core.OnConnected(endPoint);
            }

            void IRawReliableHandler.OnDisconnected(StopReason reason)
            {
                ChangeState(HandlerState.Connected, HandlerState.Disconnected);
                _core.OnDisconnected(reason);
            }

            void IRawHandler.OnStopped(StopReason reason)
            {
                // TODO: implement ChangeState() check
                _core.OnStopped(reason);
            }

            void IRawHandler.OnReceived(UnionDataList receivedBuffer)
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
