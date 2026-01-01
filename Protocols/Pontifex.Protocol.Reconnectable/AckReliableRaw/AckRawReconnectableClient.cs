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
        private readonly IPeriodicLogicRunner? _reconnectableSharedLogicRunner;

        private ReconnectableClientLogic? _logic;
        private ILogicDriverCtl? _logicDriver;

        public AckRawReconnectableClient(
            Func<IAckReliableRawClient?> underlyingTransportProducer,
            TimeSpan disconnectTimeout,
            ILogger logger,
            IMemoryRental memoryRental,
            IPeriodicLogicRunner? reconnectableSharedLogicRunner = null)
            : base(ReconnectableInfo.TransportName, logger, memoryRental)
        {
            _underlyingTransportProducer = underlyingTransportProducer;
            _disconnectTimeout = disconnectTimeout;
            _reconnectableSharedLogicRunner = reconnectableSharedLogicRunner;
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

            var logicTickTime = DeltaTime.FromMiliseconds(20);

            if (_reconnectableSharedLogicRunner != null)
            {
                _logicDriver = _reconnectableSharedLogicRunner.Run(_logic, logicTickTime);
            }
            else
            {
                var threadedDriver = new PeriodicLogicThreadedDriver(logicTickTime, 128);
                threadedDriver.Start(_logic, Log);
                _logicDriver = threadedDriver;
            }

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
                _logicDriver?.Stop();
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