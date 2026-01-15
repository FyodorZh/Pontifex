using Actuarius.Memory;
using Operarius;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Abstractions.Handlers;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public abstract class AnySideApi : IRawBaseHandler
    {
        private readonly TransportPipeSystem _transportPipeSystem;
        private readonly ProtocolApi _api;
        
        private IAckRawBaseEndpoint? _endpoint;

        private volatile StopReason _currentReasonToStop = StopReason.Void;
        
        protected AnySideApi(bool isServerMode, ProtocolApi api, IMemoryRental memoryRental)
        {
            _transportPipeSystem = new TransportPipeSystem(Send, memoryRental);
            _api = api;
            api.Prepare(isServerMode, _transportPipeSystem);
        }

        private SendResult Send(UnionDataList data)
        {
            return _endpoint?.Send(data) ?? SendResult.NotConnected;
        }

        protected void OnConnected(IAckRawBaseEndpoint endpoint)
        {
            _endpoint = endpoint;
        }
        
        /// <summary>
        /// Асинхронно, но максимально быстро разрушает подключение.
        /// Удалённый контрагент остаётся в неведении. Вероятно будет отключен по своему таймауту.
        /// </summary>
        public void Stop(StopReason? reason = null)
        {
            reason ??= StopReason.UserIntention;
            System.Threading.Interlocked.CompareExchange(ref _currentReasonToStop, reason, StopReason.Void);
        }

        void IRawBaseHandler.OnDisconnected(StopReason reason)
        {
            System.Threading.Interlocked.CompareExchange(ref _currentReasonToStop, reason, StopReason.Void);

            _endpoint = null;
            //OnDisconnected(reason);
            Stop();
        }

        void IRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
        {
            _transportPipeSystem.OnReceived(receivedBuffer);
        }
    }
}