using System;
using System.Threading;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Pending state of one RR unreliable client request: the bound handler, the message count fixed
    /// by the first response frame, and the message ids already delivered.
    /// </summary>
    internal sealed class RRUnreliableRequestState
    {
        private const int SeenWords = RRUnreliableFrame.MaxMessageCount / 64;
        private readonly ulong[] _seen = new ulong[SeenWords];
        private int _messageCount = -1;
        private int _terminal;
        private int _callbackActive;
        private int _published;

        public RRUnreliableRequestState(ushort requestId, IRRUnreliableRequestHandler handler)
        {
            RequestId = requestId;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// Gets the client-local id of the request.
        /// </summary>
        public ushort RequestId { get; }

        /// <summary>
        /// Gets the handler bound to the request.
        /// </summary>
        public IRRUnreliableRequestHandler Handler { get; }

        /// <summary>
        /// Gets the synchronization object that serializes the delivery of this request.
        /// </summary>
        public object SyncRoot { get; } = new object();

        /// <summary>
        /// Gets the declared message count, or <c>-1</c> before the first frame fixes it.
        /// </summary>
        public int MessageCount => Volatile.Read(ref _messageCount);

        /// <summary>
        /// Gets whether the state no longer accepts deliveries.
        /// </summary>
        public bool IsTerminal => Volatile.Read(ref _terminal) != 0;

        /// <summary>
        /// Gets whether the handler has published the request id. A frame that arrives before this
        /// point cannot be delivered because the handler does not identify the request yet.
        /// </summary>
        public bool IsPublished => Volatile.Read(ref _published) != 0;

        /// <summary>
        /// Marks the request id as published.
        /// </summary>
        public void MarkPublished()
        {
            Volatile.Write(ref _published, 1);
        }

        /// <summary>
        /// Gets whether a callback for this request is currently running.
        /// </summary>
        public bool IsCallbackActive => Volatile.Read(ref _callbackActive) != 0;

        /// <summary>
        /// Marks a callback as running. Returns <see langword="false"/> when another callback is
        /// already running or the state is terminal.
        /// </summary>
        public bool TryEnterCallback()
        {
            if (Interlocked.CompareExchange(ref _callbackActive, 1, 0) != 0)
            {
                return false;
            }

            if (IsTerminal)
            {
                ExitCallback();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Marks the callback as finished.
        /// </summary>
        /// <returns><see langword="true"/> when the state became terminal while the callback ran,
        /// so the caller must unbind the handler.</returns>
        public bool ExitCallback()
        {
            Interlocked.Exchange(ref _callbackActive, 0);
            return IsTerminal;
        }

        /// <summary>
        /// Fixes the message count or verifies it against the value fixed earlier. The caller holds
        /// <see cref="SyncRoot"/>.
        /// </summary>
        public bool TryFixMessageCount(int messageCount)
        {
            if (_messageCount < 0)
            {
                Volatile.Write(ref _messageCount, messageCount);
                return true;
            }

            return _messageCount == messageCount;
        }

        /// <summary>
        /// Marks the message id as delivered. The caller holds <see cref="SyncRoot"/>.
        /// </summary>
        /// <returns><see langword="true"/> for the first delivery of the id; otherwise
        /// <see langword="false"/>.</returns>
        public bool TryMarkSeen(int messageId)
        {
            int word = messageId >> 6;
            ulong bit = 1UL << (messageId & 63);

            if ((_seen[word] & bit) != 0)
            {
                return false;
            }

            _seen[word] |= bit;
            return true;
        }

        /// <summary>
        /// Marks the state terminal. Returns <see langword="true"/> for the first call.
        /// </summary>
        public bool TryTerminate()
        {
            return Interlocked.Exchange(ref _terminal, 1) == 0;
        }
    }
}
