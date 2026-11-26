using System;
using System.Threading;
using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Server-side handle of one RR unreliable request. It owns the response route and sends a
    /// declared number of messages, assigning their zero-based ids in call order.
    /// </summary>
    internal sealed class RRUnreliableResponse : RRResponse, IRRUnreliableResponse
    {
        private readonly RRUnreliableServerTransport _transport;
        private readonly IEndPoint _client;
        private readonly ushort _clientRequestId;
        private readonly DateTime _expiresAt;
        private readonly object _gate = new object();
        private int _messageCount = 1;
        private int _nextMessageId;
        private bool _declarationLocked;
        private int _closed;

        public RRUnreliableResponse(RRUnreliableServerTransport transport, long requestId,
            DateTime expiresAt, IEndPoint client, ushort clientRequestId)
            : base(requestId)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _expiresAt = expiresAt;
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _clientRequestId = clientRequestId;
        }

        /// <summary>
        /// Gets the route that received the request.
        /// </summary>
        public IEndPoint Client => _client;

        /// <summary>
        /// Gets the 16-bit client-local request id that the response frames must carry back.
        /// </summary>
        public ushort ClientRequestId => _clientRequestId;

        /// <summary>
        /// Gets the moment after which the handle no longer answers.
        /// </summary>
        public DateTime ExpiresAt => _expiresAt;

        /// <summary>
        /// Gets the id the next accepted send carries.
        /// </summary>
        internal int NextMessageId => Volatile.Read(ref _nextMessageId);

        /// <inheritdoc/>
        public int MessageCount => Volatile.Read(ref _messageCount);

        /// <summary>
        /// Gets whether the handle is already closed.
        /// </summary>
        public bool IsClosed => Volatile.Read(ref _closed) != 0;

        /// <inheritdoc/>
        public void SetMessageCount(int messageCount)
        {
            if (messageCount < 1 || messageCount > RRUnreliableFrame.MaxMessageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(messageCount),
                    "The message count must be in the range 1..256.");
            }

            bool canAnswer = _transport.CanAnswer(this);

            lock (_gate)
            {
                if (_declarationLocked || IsClosed || !canAnswer || !_transport.IsRunningUnsafe ||
                    _transport.HasExpired(this))
                {
                    throw new InvalidOperationException(
                        "The message count must be set once, before the first response is sent, " +
                        "while the handle can answer.");
                }

                _messageCount = messageCount;
                _declarationLocked = true;
            }
        }

        /// <inheritdoc/>
        public override SendResult Response(UnionDataList response)
        {
            bool canAnswer = _transport.CanAnswer(this);

            lock (_gate)
            {
                _declarationLocked = true;
                return _transport.SendResponse(this, response, canAnswer);
            }
        }

        /// <summary>
        /// Marks the message as accepted, advances the next id, and closes the handle after the
        /// declared last message.
        /// </summary>
        internal void CommitMessage()
        {
            lock (_gate)
            {
                int next = _nextMessageId + 1;
                _nextMessageId = next;

                if (next >= _messageCount)
                {
                    Interlocked.Exchange(ref _closed, 1);
                }
            }
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
