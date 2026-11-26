using System;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.RR.Direct;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.RR.Reliable.Direct
{
    /// <summary>
    /// In-process RR reliable client. The transport owns one fixed client endpoint and a lazy channel
    /// carrier. A channel loss before the first successful send is not a transport failure; a loss
    /// after a live session fails the transport.
    /// </summary>
    internal sealed class RRReliableDirectClient : RRReliableClientTransport
    {
        private readonly IEndPoint _clientEndPoint;

        private DirectRrChannelClient? _carrier;
        private volatile bool _hasLiveSession;

        public RRReliableDirectClient(
            IEndPoint serverEndPoint,
            TimeSpan requestTtl,
            ILogger logger,
            IMemoryRental memory,
            IRRReliableTimerFactory? timerFactory = null)
            : base(RRDirectInfo.ReliableTransportName + "_rr", serverEndPoint, requestTtl, logger,
                memory, timerFactory)
        {
            _clientEndPoint = new GuidEndPoint(Guid.NewGuid());
        }

        public override int MessageMaxByteSize => RRDirectInfo.MessageMaxByteSize;

        /// <summary>
        /// Gets the carrier of this client, or <see langword="null"/> before the start or after the
        /// stop.
        /// </summary>
        internal DirectRrChannelClient? CurrentCarrier => _carrier;

        protected override bool StartCarrier()
        {
            _hasLiveSession = false;
            _carrier = new DirectRrChannelClient(
                RRDirectInfo.ReliableTransportName,
                ServerEndPoint,
                _clientEndPoint,
                Log,
                frame => OnFrameReceived(ServerEndPoint, frame),
                OnCarrierDisposed,
                OnLiveSession);

            return true;
        }

        protected override void StopCarrier(StopReason reason)
        {
            DirectRrChannelClient? carrier = _carrier;
            _carrier = null;
            _hasLiveSession = false;
            carrier?.Stop();
        }

        protected override SendResult SendFrame(UnionDataList frame)
        {
            DirectRrChannelClient? carrier = _carrier;
            if (carrier == null)
            {
                frame.Release();
                return SendResult.Error;
            }

            return carrier.TrySend(frame, acceptWithoutPeer: false);
        }

        private void OnLiveSession()
        {
            _hasLiveSession = true;
        }

        private void OnCarrierDisposed()
        {
            // A channel that disappears before the first successful send never established a
            // session. The carrier has already cleared it, so a later request retries.
            if (!_hasLiveSession)
            {
                return;
            }

            // A normal stop clears the started state before it disposes the carrier, so this guard
            // distinguishes a loss from an orderly teardown without taking the carrier lock.
            if (!IsStarted)
            {
                return;
            }

            // Schedule the terminal notification before the transport reports the failure. The
            // callback itself is never invoked under the carrier lock.
            ClearPendingRequests();
            Fail(new TextFail(Name, "direct channel disposed"));
        }

        public override string ToString()
        {
            try
            {
                return $"direct-rr-reliable-client[{ServerEndPoint}]";
            }
            catch (Exception)
            {
                return "direct-rr-reliable-client[unknown]";
            }
        }
    }
}
