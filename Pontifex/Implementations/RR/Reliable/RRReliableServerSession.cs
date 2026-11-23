using System;
using System.Collections.Generic;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Per-client duplicate-suppression state of one reliable RR server.
    /// </summary>
    /// <remarks>
    /// The live map is keyed by the client-local request id and holds the response handles that can
    /// still answer. The completed window is a 65,536-bit bitmap over the same id domain: it records
    /// every terminal id, so a retransmitted request after its completion is suppressed. The window
    /// is per session and is never shared between clients. When it is full the server rejects new
    /// requests, and a fresh client transport is required to re-establish the session.
    /// </remarks>
    internal sealed class RRReliableServerSession
    {
        private const int IdCount = 1 << 16;

        private readonly byte[] _completed = new byte[IdCount / 8];
        private readonly Dictionary<ushort, RRReliableResponse> _live =
            new Dictionary<ushort, RRReliableResponse>();

        private int _completedCount;

        /// <summary>
        /// Gets the lock that guards the session maps.
        /// </summary>
        public object SyncRoot { get; } = new object();

        /// <summary>
        /// Gets whether the session is closed. A closed session never presents another request.
        /// </summary>
        public bool IsClosed { get; private set; }

        /// <summary>
        /// Gets whether the completed window already covers every client-local id.
        /// </summary>
        public bool IsFull => _completedCount >= IdCount;

        /// <summary>
        /// Gets whether a response handle is live for <paramref name="clientLocalId"/>.
        /// </summary>
        public bool IsLive(ushort clientLocalId)
        {
            return _live.ContainsKey(clientLocalId);
        }

        /// <summary>
        /// Gets whether <paramref name="clientLocalId"/> already reached a terminal state.
        /// </summary>
        public bool IsCompleted(ushort clientLocalId)
        {
            int index = clientLocalId >> 3;
            int mask = 1 << (clientLocalId & 7);
            return (_completed[index] & mask) != 0;
        }

        /// <summary>
        /// Adds a live response handle.
        /// </summary>
        public void AddLive(ushort clientLocalId, RRReliableResponse response)
        {
            _live.Add(clientLocalId, response);
        }

        /// <summary>
        /// Removes the live handle of <paramref name="clientLocalId"/>.
        /// </summary>
        public bool TryTakeLive(ushort clientLocalId, out RRReliableResponse response)
        {
            if (_live.Remove(clientLocalId, out RRReliableResponse? stored))
            {
                response = stored;
                return true;
            }

            response = null!;
            return false;
        }

        /// <summary>
        /// Removes the live handle only when it is the expected instance.
        /// </summary>
        public bool TryTakeLive(ushort clientLocalId, RRReliableResponse expected)
        {
            if (_live.TryGetValue(clientLocalId, out RRReliableResponse? stored) &&
                ReferenceEquals(stored, expected))
            {
                _live.Remove(clientLocalId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Records one terminal client-local id and removes its live handle.
        /// </summary>
        /// <param name="clientLocalId">The terminal id.</param>
        /// <param name="expected">The handle that reports the terminal, or <see langword="null"/>
        /// when no live handle is expected.</param>
        public void MarkCompleted(ushort clientLocalId, RRReliableResponse? expected)
        {
            if (expected == null)
            {
                _live.Remove(clientLocalId);
            }
            else
            {
                TryTakeLive(clientLocalId, expected);
            }

            int index = clientLocalId >> 3;
            int mask = 1 << (clientLocalId & 7);

            if ((_completed[index] & mask) == 0)
            {
                _completed[index] = (byte)(_completed[index] | mask);

                if (_completedCount < IdCount)
                {
                    _completedCount++;
                }
            }
        }

        /// <summary>
        /// Closes the session and returns every live handle. The caller marks the handles closed
        /// outside the session lock.
        /// </summary>
        public RRReliableResponse[] Close()
        {
            if (IsClosed)
            {
                return Array.Empty<RRReliableResponse>();
            }

            IsClosed = true;
            var handles = new RRReliableResponse[_live.Count];
            _live.Values.CopyTo(handles, 0);
            _live.Clear();
            return handles;
        }
    }
}
