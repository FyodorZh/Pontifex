using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Reconnectable
{
    class ReconnectableServerLogic : ReconnectableBaseLogic<IRawReliableEndpoint>, IRawReliableAckServerHandler, IRawReliableEndpoint
    {
        private IMultiRefReadOnlyByteArray? _secret;

        private readonly IRawReliableAckServerHandler mUserHandler;

        private readonly object _ackResponseLock = new();

        private UnionDataList? _ackResponse;

        private volatile bool mAttached;

        public event Action<IRawReliableEndpoint>? OnConnected;

        public ReconnectableServerLogic(IRawReliableAckServerHandler userHandler, TimeSpan disconnectTimeout, ILogger logger, IMemoryRental memoryRental)
            : base(userHandler, disconnectTimeout, logger, memoryRental)
        {
            mUserHandler = userHandler;
        }

        protected override bool BeginReconnect()
        {
            return false; // ждём клиента, не делаем ничего
        }

        public override void OnDisconnected(StopReason reason)
        {
            OnConnectionStopped(reason);
            mAttached = false;
        }

        void IRawHandler.OnStopped(StopReason reason)
        {
            // do nothing ???
        }

        public bool Attach(SessionId sessionId)
        {
            if (sessionId.IsValid)
            {
                _sessionId = sessionId;
                _secret = new StaticReadOnlyByteArray(Guid.NewGuid().ToByteArray());
                mAttached = true;
                return true;
            }
            return false;
        }

        public bool Reattach(IMultiRefReadOnlyByteArray secret)
        {
            using var secretDisposer = secret.AsDisposable();
            if (mAttached)
            {
                Log.w("{sessionId}: Reattach failed due to true multiple reconnection", this);
                return false;
            }

            if (!_secret.EqualByContent(secret))
            {
                Log.w("{sessionId}: Reattach failed due to secret difference. '{secret}' vs '{newSecret}'", this, _secret?.ToString() ?? "null", secret);
                return false;
            }

            mAttached = true;
            return true;
        }

        /// <summary>
        /// Composes the ACK response for the newly attached session: the reconnectable metadata
        /// followed by the user response payload. Takes ownership of <paramref name="userAckResponse"/>
        /// and releases it before returning.
        /// </summary>
        /// <param name="userAckResponse">The ACK response composed by the user acknowledger.</param>
        /// <returns>The composed ACK response owned by the caller; its clone is kept for reattach.</returns>
        public UnionDataList CreateAckResponse(UnionDataList userAckResponse)
        {
            UnionDataList? response = null;
            try
            {
                response = Memory.CollectablePool.Acquire<UnionDataList>();
                response.CopyFrom(userAckResponse);
                response.PutFirst(new UnionData(_secret));
                response.PutFirst(new UnionData(_sessionId.Generation));
                response.PutFirst(new UnionData(_sessionId.Id));
                response.PutFirst(new UnionData(ReconnectableInfo.AckOKResponse));

                var storedResponse = response.Clone(Memory.CollectablePool);
                lock (_ackResponseLock)
                {
                    _ackResponse = storedResponse;
                }

                return response;
            }
            catch
            {
                response?.Release();
                throw;
            }
            finally
            {
                userAckResponse.Release();
            }
        }

        /// <summary>
        /// Clones the ACK response composed for the original connection so that a reattached session
        /// answers byte-for-byte identically.
        /// </summary>
        /// <returns>A fresh ACK response owned by the caller, or <see langword="null"/> when the
        /// session response is already released.</returns>
        public UnionDataList? CloneAckResponse()
        {
            lock (_ackResponseLock)
            {
                return _ackResponse?.Clone(Memory.CollectablePool);
            }
        }

        /// <summary>
        /// Releases the ACK response kept for reattach. Idempotent.
        /// </summary>
        public void ReleaseAckResponse()
        {
            UnionDataList? response;
            lock (_ackResponseLock)
            {
                response = _ackResponse;
                _ackResponse = null;
            }

            response?.Release();
        }

        void IRawReliableServerHandler.OnConnected(IRawReliableEndpoint endPoint)
        {
            Connect(endPoint, out var isFirstConnection);
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