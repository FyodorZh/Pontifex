using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Api
{
    /// <summary>
    /// Client-side Api handler over a reliable raw transport.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Admission gate</b>: the handler starts an Api negotiation
    /// (<c>"api"</c>, version <c>1</c>) as soon as the transport reports <c>OnConnected</c>. The Api is
    /// started and <see cref="Connected"/> is raised only after the server accepts the negotiation; inbound
    /// application data is never delivered before that point.</description></item>
    /// <item><description><b>Lifecycle</b>: <see cref="Disconnected"/> is raised exactly once. The
    /// <c>OnDisconnected</c> and <c>OnStopped</c> callbacks of the transport both report the terminal event, so
    /// a single one-shot latch deduplicates them.</description></item>
    /// </list>
    /// </remarks>
    public class ClientSideApi : IRawReliableClientHandler
    {
        private const short NegotiationVersion = 1;
        private const string NegotiationName = "api";

        private readonly IApiRoot _api;
        private readonly IMemoryRental _memoryRental;
        private readonly ILogger Log;
        private readonly ITimerFactory? _negotiationTimerFactory;

        private IRawReliableEndpoint? _endpoint;
        private NegotiationSession? _negotiation;
        private TransportPipeSystem? _transportPipeSystem;

        private int _admitted;
        private int _disconnectedRaised;

        public event Action<IRawReliableEndpoint>? Connected;
        public event Action<StopReason>? Disconnected;

        /// <summary>
        /// Appends custom elements to the Api negotiation request.
        /// </summary>
        /// <param name="customTail">A session-owned tail. The hook is append-only: it must neither retain nor
        /// release the list. The Api server currently ignores appended elements.</param>
        protected virtual void AppendNegotiationData(UnionDataList customTail)
        {
            // Override to append custom negotiation data.
        }

        public ClientSideApi(IApiRoot api, IMemoryRental memoryRental, ILogger logger)
            : this(api, memoryRental, logger, null)
        {
        }

        /// <summary>
        /// Creates the client Api logic with an explicit timer seam. Used by deterministic tests; the
        /// public constructor selects the system timer.
        /// </summary>
        internal ClientSideApi(
            IApiRoot api,
            IMemoryRental memoryRental,
            ILogger logger,
            ITimerFactory? negotiationTimerFactory)
        {
            _api = api;
            _memoryRental = memoryRental;
            Log = logger;
            _negotiationTimerFactory = negotiationTimerFactory;
        }

        void IRawReliableClientHandler.OnConnected(IRawReliableEndpoint endPoint)
        {
            _endpoint = endPoint;

            var negotiation = new NegotiationSession(
                NegotiationRole.Client,
                NegotiationVersion,
                NegotiationName,
                SendEnvelope,
                RejectNegotiation,
                _ => { },
                HandleNegotiationAccepted,
                appendData: AppendNegotiationData,
                timerFactory: _negotiationTimerFactory,
                log: Log);

            _negotiation = negotiation;
            negotiation.Start();
        }

        void IRawHandler.OnReceived(UnionDataList receivedBuffer)
        {
            if (Volatile.Read(ref _admitted) == 0)
            {
                var negotiation = _negotiation;
                if (negotiation != null && negotiation.OnReceived(receivedBuffer))
                {
                    return;
                }

                receivedBuffer.Release();
                RejectNegotiation(new ProtocolMismatch(NegotiationName,
                    "unexpected payload before the api negotiation completed"));
                return;
            }

            var pipeSystem = _transportPipeSystem;
            if (pipeSystem == null)
            {
                receivedBuffer.Release();
                return;
            }

            pipeSystem.OnReceived(receivedBuffer);
        }

        void IRawReliableHandler.OnDisconnected(StopReason reason)
        {
            _api.Stop();
            _transportPipeSystem = null;
            _endpoint = null;
            DisposeNegotiation();
            RaiseDisconnected(reason);
        }

        void IRawHandler.OnStopped(StopReason reason)
        {
            _api.Stop();
            _transportPipeSystem = null;
            _endpoint = null;
            DisposeNegotiation();
            RaiseDisconnected(reason);
        }

        private void HandleNegotiationAccepted(UnionDataList customTail)
        {
            if (Interlocked.Exchange(ref _admitted, 1) != 0)
            {
                return;
            }

            var pipeSystem = new TransportPipeSystem(dataToSend =>
            {
                var endpoint = _endpoint;
                if (endpoint != null)
                {
                    return endpoint.Send(dataToSend);
                }

                dataToSend.Release();
                return SendResult.NotConnected;
            }, _memoryRental, Log);

            _transportPipeSystem = pipeSystem;
            _api.Disconnected += r => _endpoint?.Disconnect(r);
            _api.Start(false, pipeSystem);

            var endpoint = _endpoint;
            if (endpoint != null)
            {
                Connected?.Invoke(endpoint);
            }
        }

        private void RejectNegotiation(StopReason reason)
        {
            if (Interlocked.Exchange(ref _disconnectedRaised, 1) != 0)
            {
                return;
            }

            var endpoint = _endpoint;
            endpoint?.Disconnect(reason);

            _api.Stop();
            _transportPipeSystem = null;
            DisposeNegotiation();

            Disconnected?.Invoke(reason);
        }

        private void RaiseDisconnected(StopReason reason)
        {
            if (Interlocked.Exchange(ref _disconnectedRaised, 1) != 0)
            {
                return;
            }

            Disconnected?.Invoke(reason);
        }

        private void DisposeNegotiation()
        {
            var negotiation = Interlocked.Exchange(ref _negotiation, null);
            negotiation?.Dispose();
        }

        private SendResult SendEnvelope(UnionDataList envelope)
        {
            var endpoint = _endpoint;
            if (endpoint == null)
            {
                envelope.Release();
                return SendResult.NotConnected;
            }

            try
            {
                return endpoint.Send(envelope);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return SendResult.Error;
            }
        }
    }
}
