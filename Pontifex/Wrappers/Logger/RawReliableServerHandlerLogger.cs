using System;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Logger
{
    /// <summary>
    /// Logs the server-handler callbacks and forwards them to the user handler, wrapping the
    /// endpoint handed to <see cref="OnConnected"/> so that <c>Send</c>/<c>Disconnect</c> are logged
    /// too.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>The wrapper serves the reliable server logger; the user handler may be swapped
    /// between sessions through <see cref="UserHandler"/>.
    /// The wrapper itself is not thread-safe; the owning transport serializes handler
    /// callbacks.</item>
    /// <item>All messages go to the transport logger supplied in the constructor, never to the
    /// static <see cref="Scriba.Log"/> sink.</item>
    /// </list>
    /// </remarks>
    internal sealed class RawReliableServerHandlerLogger :
        IRawReliableServerHandler
    {
        private readonly ILogger _log;

        public RawReliableServerHandlerLogger(ILogger log, IRawReliableServerHandler? userHandler)
        {
            _log = log;
            UserHandler = userHandler;
        }

        /// <summary>
        /// Gets or sets the user handler that receives the forwarded callbacks.
        /// </summary>
        public IRawReliableServerHandler? UserHandler { get; set; }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            _log.i("UserHandler.OnConnected()");
            var endPointWrapper = new RawReliableServerSideEndpointWrapper(endPoint, (endpoint, dataToSend) =>
            {
                _log.i("EndPoint.Send(" + dataToSend + ")");
                if (endpoint == null)
                {
                    return SendResult.Error;
                }

                var res = endpoint.Send(dataToSend);
                _log.i("Result: " + res);
                return res;
            }, (endpoint, disconnectReason) =>
            {
                _log.i("EndPoint.Disconnect(" + disconnectReason + ")");
                return endpoint?.Disconnect(disconnectReason) ?? false;
            },
                Array.Empty<IControl>());
            UserHandler?.OnConnected(endPointWrapper);
        }

        public void OnDisconnected(StopReason reason)
        {
            _log.i("UserHandler.OnDisconnected(" + reason + ")");
            UserHandler?.OnDisconnected(reason);
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            _log.i("UserHandler.OnReceived(" + receivedBuffer + ")");
            UserHandler?.OnReceived(receivedBuffer);
        }

        public void OnStopped(StopReason reason)
        {
            _log.i("UserHandler.OnStopped(" + reason + ")");
            UserHandler?.OnStopped(reason);
        }
    }
}
