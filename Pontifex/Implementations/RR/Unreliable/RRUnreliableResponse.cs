using System;
using System.Threading;
using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Server-side handle of one RR unreliable request. It owns the response route and is single-use.
    /// </summary>
    internal sealed class RRUnreliableResponse : RRResponse, IRRUnreliableResponse
    {
        private readonly RRUnreliableServerTransport _transport;
        private readonly IEndPoint _client;
        private readonly long _clientRequestId;
        private readonly DateTime _expiresAt;
        private int _closed;

        public RRUnreliableResponse(RRUnreliableServerTransport transport, long requestId,
            DateTime expiresAt, IEndPoint client, long clientRequestId)
            : base(requestId)
        {
            _transport = transport;
            _expiresAt = expiresAt;
            _client = client;
            _clientRequestId = clientRequestId;
        }

        /// <summary>
        /// Gets the route that received the request.
        /// </summary>
        public IEndPoint Client => _client;

        /// <summary>
        /// Gets the client-local id that the response frame must carry back.
        /// </summary>
        public long ClientRequestId => _clientRequestId;

        /// <summary>
        /// Gets the moment after which the handle no longer answers.
        /// </summary>
        public DateTime ExpiresAt => _expiresAt;

        /// <summary>
        /// Gets whether the handle is already closed.
        /// </summary>
        public bool IsClosed => Volatile.Read(ref _closed) != 0;

        /// <inheritdoc/>
        public override SendResult Response(UnionDataList response)
        {
            return _transport.SendResponse(this, response);
        }

        /// <summary>
        /// Closes the handle if it is open. Returns <see langword="true"/> for the closing call.
        /// </summary>
        public bool TryClose()
        {
            return Interlocked.Exchange(ref _closed, 1) == 0;
        }

        /// <summary>
        /// Closes the handle.
        /// </summary>
        public void Close()
        {
            Interlocked.Exchange(ref _closed, 1);
        }
    }
}
