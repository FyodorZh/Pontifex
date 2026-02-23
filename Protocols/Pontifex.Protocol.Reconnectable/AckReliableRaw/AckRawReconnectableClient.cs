using System;
using Actuarius.Memory;
using Operarius;
using Pontifex.Abstractions.Clients;
using Pontifex.Transports.Core;
using Scriba;

namespace Pontifex.Protocols.Reconnectable.AckReliableRaw
{
    public sealed class AckRawReconnectableClient : AckRawClient, IAckReliableRawClient
    {
        private readonly Func<IAckReliableRawClient?> _underlyingTransportProducer;
        private readonly TimeSpan _disconnectTimeout;

        private ReconnectableClientLogic? _logic;
        private  ILogicDriver<IPeriodicLogicDriverCtl>? _logicDriver;

        public AckRawReconnectableClient(
            Func<IAckReliableRawClient?> underlyingTransportProducer,
            TimeSpan disconnectTimeout,
            ILogger logger,
            IMemoryRental memoryRental)
            : base(ReconnectableInfo.TransportName, logger, memoryRental)
        {
            _underlyingTransportProducer = underlyingTransportProducer;
            _disconnectTimeout = disconnectTimeout;
        }

        protected override bool BeginConnect()
        {
            if (Handler == null)
            {
                Log.e("Handler is not set");
                return false;
            }
            
            _logic = new ReconnectableClientLogic(_underlyingTransportProducer, Handler, _disconnectTimeout, Log, Memory);

            _logic.OnConnected += ConnectionFinished;

            _logic.OnStopped += (reason) => { Stop(reason); };

            var logicTickTime = TimeSpan.FromMilliseconds(20);

            var threadedDriver = new ThreadBasedPeriodicMultiLogicDriver(NowDateTimeProvider.Instance, logicTickTime);
            threadedDriver.Start(_logic);
            _logicDriver = threadedDriver;

            return true;
        }

        protected override void OnReadyToConnect()
        {
            // TODO: Fix possible race
        }

        protected override void DestroyTransport(StopReason reason)
        {
            var logic = _logic;
            if (logic != null)
            {
                _logicDriver?.Finish();
            }

            _logic = null;
        }

        public override int MessageMaxByteSize => _logic?.MessageMaxByteSize ?? 0;

        public override string ToString()
        {
            var logic = _logic;
            if (logic != null)
            {
                return "reconnectable-client[" + logic.SessionId + "]";
            }

            return "reconnectable-client[null]";
        }
    }
}