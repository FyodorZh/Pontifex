using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Api
{
    /// <summary>
    /// One server-side Api session over an admitted reliable raw transport session.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Admission gate</b>: the session consumes the first inbound payload as the Api
    /// negotiation request (<c>"api"</c>, version <c>1</c>). The Api is started and
    /// <see cref="ApiStarted"/> is raised only after the accepted response has been sent; inbound
    /// application data is never delivered before that point.</description></item>
    /// <item><description><b>Dataset</b>: the Api server compares only the name and version and ignores any
    /// appended negotiation elements, which are reserved for future metadata.</description></item>
    /// </list>
    /// </remarks>
    public class ServerSideApiInstance<TApi> : IRawReliableServerHandler
        where TApi : IApiRoot
    {
        private const short NegotiationVersion = 1;
        private const string NegotiationName = "api";

        private readonly TApi _api;
        private readonly IMemoryRental _memoryRental;
        private readonly ILogger Log;

        private IRawReliableEndpoint? _endpoint;
        private NegotiationSession? _negotiation;
        private TransportPipeSystem? _transportPipeSystem;

        private int _admitted;

        public event Action<ServerSideApiInstance<TApi>>? ApiStarted;
        public event Action<ServerSideApiInstance<TApi>>? ApiStopped;

        protected TApi Api => _api;

        public IEndPoint Endpoint => _endpoint?.RemoteEndPoint ?? VoidEndPoint.Instance;

        public ServerSideApiInstance(TApi api, IMemoryRental memoryRental, ILogger logger)
        {
            _api = api;
            _memoryRental = memoryRental;
            Log = logger;
        }

        void IRawReliableServerHandler.OnConnected(IRawReliableEndpoint endPoint)
        {
            _endpoint = endPoint;

            var negotiation = new NegotiationSession(
                NegotiationRole.Server,
                NegotiationVersion,
                NegotiationName,
                SendEnvelope,
                _ => { },
                reason => endPoint.Disconnect(reason),
                HandleNegotiationAccepted,
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
                RejectBeforeAdmission();
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
            ApiStopped?.Invoke(this);
            _api.Stop();
            _transportPipeSystem = null;
            _endpoint = null;
            DisposeNegotiation();
        }

        void IRawHandler.OnStopped(StopReason reason)
        {
            _transportPipeSystem = null;
            _endpoint = null;
            DisposeNegotiation();
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
            _api.Start(true, pipeSystem);

            ApiStarted?.Invoke(this);
        }

        private void RejectBeforeAdmission()
        {
            _negotiation?.Reject(
                new ProtocolMismatch(NegotiationName, "expected the api negotiation request"),
                "expected the api negotiation request");
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
