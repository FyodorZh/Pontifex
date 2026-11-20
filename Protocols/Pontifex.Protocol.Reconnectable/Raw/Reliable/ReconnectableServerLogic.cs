using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Reconnectable
{
    /// <summary>
    /// The persistent server half of a reconnectable session. It owns the session secret, the negotiation
    /// response kept for reattach, and the delivery system shared by every underlying connection.
    /// </summary>
    internal sealed class ReconnectableServerLogic : ReconnectableBaseLogic<IRawReliableEndpoint>,
        IRawReliableServerHandler, IRawReliableEndpoint
    {
        private IMultiRefReadOnlyByteArray? _secret;

        private readonly object _responseLock = new object();

        private UnionDataList? _negotiationResponse;

        private volatile bool _attached;

        public event Action<IRawReliableEndpoint>? OnConnected;

        public ReconnectableServerLogic(IRawReliableServerHandler userHandler, TimeSpan disconnectTimeout,
            ILogger logger, IMemoryRental memoryRental)
            : base(userHandler, disconnectTimeout, logger, memoryRental)
        {
        }

        protected override bool BeginReconnect()
        {
            return false; // The server waits for the client to come back.
        }

        public override void OnDisconnected(StopReason reason)
        {
            OnConnectionStopped(reason);
            _attached = false;
        }

        public bool Attach(SessionId sessionId)
        {
            if (!sessionId.IsValid)
            {
                return false;
            }

            _sessionId = sessionId;
            _secret = new StaticReadOnlyByteArray(Guid.NewGuid().ToByteArray());
            _attached = true;
            return true;
        }

        public bool Reattach(IMultiRefReadOnlyByteArray secret)
        {
            using var secretDisposer = secret.AsDisposable();
            if (_attached)
            {
                Log.w("{sessionId}: Reattach failed due to true multiple reconnection", this);
                return false;
            }

            if (!_secret.EqualByContent(secret))
            {
                Log.w("{sessionId}: Reattach failed due to secret difference. '{secret}' vs '{newSecret}'",
                    this, _secret?.ToString() ?? "null", secret);
                return false;
            }

            _attached = true;
            return true;
        }

        /// <summary>
        /// Builds the reconnectable negotiation response tail: the session id, its generation, and the
        /// session secret. The returned list is owned by the caller; a clone is kept for reattach.
        /// </summary>
        public UnionDataList CreateNegotiationResponse()
        {
            UnionDataList? response = null;
            try
            {
                if (_secret == null)
                {
                    throw new InvalidOperationException("The session secret is not set.");
                }

                response = Memory.CollectablePool.Acquire<UnionDataList>();
                response.PutLast(new UnionData(_sessionId.Id));
                response.PutLast(new UnionData(_sessionId.Generation));
                response.PutLast(new UnionData(_secret));

                var storedResponse = response.Clone(Memory.CollectablePool);
                UnionDataList? previous;
                lock (_responseLock)
                {
                    previous = _negotiationResponse;
                    _negotiationResponse = storedResponse;
                }

                previous?.Release();
                return response;
            }
            catch
            {
                response?.Release();
                throw;
            }
        }

        /// <summary>
        /// Clones the negotiation response built for the original connection so a reattached session
        /// answers identically.
        /// </summary>
        /// <returns>A fresh response owned by the caller, or <see langword="null"/> when the session
        /// response is already released.</returns>
        public UnionDataList? CloneNegotiationResponse()
        {
            lock (_responseLock)
            {
                return _negotiationResponse?.Clone(Memory.CollectablePool);
            }
        }

        /// <summary>
        /// Releases the negotiation response kept for reattach. Idempotent.
        /// </summary>
        public void ReleaseNegotiationResponse()
        {
            UnionDataList? response;
            lock (_responseLock)
            {
                response = _negotiationResponse;
                _negotiationResponse = null;
            }

            response?.Release();
        }

        void IRawReliableServerHandler.OnConnected(IRawReliableEndpoint endPoint)
        {
            Connect(endPoint, out bool isFirstConnection);
            if (isFirstConnection)
            {
                OnConnected?.Invoke(this);
            }
        }

        public override string ToString()
        {
            return "Session[" + (_sessionId.IsValid ? _sessionId.ToString() : "invalid-session") + "]";
        }
    }
}
