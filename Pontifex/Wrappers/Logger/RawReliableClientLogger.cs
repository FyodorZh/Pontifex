using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Reliable.Ack.Logger;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Logger
{
    /// <summary>
    /// Logging wrapper for a reliable raw client transport.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Every transport call and every handler callback is logged to the wrapped
    /// transport's <see cref="ITransport.Log"/>.</item>
    /// <item>The endpoint handed to <see cref="IRawReliableClientHandler.OnConnected"/> is wrapped through
    /// <see cref="RawAckClientSideEndpointWrapper"/> so that <c>Send</c> and <c>Disconnect</c>
    /// calls are logged too.</item>
    /// <item>The wrapper is not thread-safe; the owning transport serializes handler
    /// callbacks.</item>
    /// </list>
    /// </remarks>
    public sealed class RawReliableClientLogger : IRawReliableClient, IRawReliableClientHandler
    {
        private readonly IRawReliableClient _core;

        private volatile IRawReliableClientHandler? _userHandler;

        /// <summary>
        /// Gets the transport type of the wrapped transport.
        /// </summary>
        public TransportType Type => TransportType.RawReliable;

        /// <summary>
        /// Initializes a new logging wrapper around the given client transport.
        /// </summary>
        /// <param name="core">The transport that performs the actual work.</param>
        public RawReliableClientLogger(IRawReliableClient core)
        {
            _core = core;
        }

        string ITransport.Name => _core.Name;

        bool ITransport.IsValid => _core.IsValid;

        bool ITransport.IsStarted => _core.IsStarted;

        bool ITransport.Start(Action<StopReason> onStopped)
        {
            _core.Log.i("Start()");
            return _core.Start(reason =>
            {
                _core.Log.i("OnStopped(" + reason + ")");
                onStopped(reason);
            });
        }

        bool ITransport.Stop(StopReason? reason)
        {
            _core.Log.i("Stop(" + reason + ")");
            return _core.Stop(reason);
        }

        ILogger ITransport.Log => _core.Log;

        IMemoryRental ITransport.Memory => _core.Memory;

        /// <summary>
        /// Appends the transport controls of the wrapped transport to the destination list.
        /// </summary>
        /// <param name="dst">The destination list.</param>
        /// <param name="predicate">Optional filter applied to each control.</param>
        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
            _core.GetControls(dst, predicate);
        }

        bool IRawReliableClientTransport<IRawReliableClientHandler>.Init(
            IRawReliableClientHandler handler)
        {
            _userHandler = handler;
            _core.Log.i("Init()");
            return _core.Init(this);
        }

        int IRawTransport.MessageMaxByteSize => _core.MessageMaxByteSize;

        void IRawReliableHandler.OnDisconnected(StopReason reason)
        {
            _core.Log.i("UserHandler.OnDisconnected(" + reason + ")");
            _userHandler?.OnDisconnected(reason);
        }

        void IRawHandler.OnReceived(UnionDataList receivedBuffer)
        {
            _core.Log.i("UserHandler.OnReceived(" + receivedBuffer + ")");
            _userHandler?.OnReceived(receivedBuffer);
        }

        void IRawReliableClientHandler.OnConnected(IRawReliableEndpoint endPoint)
        {
            _core.Log.i("UserHandler.OnConnected(" + endPoint + ")");
            var endPointWrapper = new RawAckClientSideEndpointWrapper(endPoint, (endpoint, dataToSend) =>
            {
                _core.Log.i("EndPoint.Send(" + dataToSend + ")");
                if (endpoint == null)
                {
                    return SendResult.Error;
                }

                var res = endpoint.Send(dataToSend);
                _core.Log.i("Result: " + res);
                return res;
            }, (endpoint, disconnectReason) =>
            {
                _core.Log.i("EndPoint.Disconnect(" + disconnectReason + ")");
                return endpoint?.Disconnect(disconnectReason) ?? false;
            });
            _userHandler?.OnConnected(endPointWrapper);
        }

        void IRawHandler.OnStopped(StopReason reason)
        {
            _core.Log.i("UserHandler.OnStopped(" + reason + ")");
            _userHandler?.OnStopped(reason: reason);
        }
    }
}
