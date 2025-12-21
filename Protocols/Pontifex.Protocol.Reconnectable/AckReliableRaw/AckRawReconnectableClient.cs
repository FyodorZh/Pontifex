using System;
using Actuarius.Memory;
using Actuarius.PeriodicLogic;
using Pontifex;
using Pontifex.Abstractions.Clients;
using Pontifex.Transports.Core;
using Scriba;

namespace Transport.Protocols.Reconnectable.AckReliableRaw
{
    public sealed class AckRawReconnectableClient : AckRawClient, IAckReliableRawClient
    {
        private readonly Func<IAckReliableRawClient> mUnderlyingTransportProducer;
        private readonly TimeSpan mDisconnectTimeout;
        private readonly IPeriodicLogicRunner? _reconnectableSharedLogicRunner;

        private ReconnectableClientLogic? mLogic;
        private ILogicDriverCtl? mLogicDriver;

        public AckRawReconnectableClient(
            Func<IAckReliableRawClient> underlyingTransportProducer,
            TimeSpan disconnectTimeout,
            ILogger? logger,
            IMemoryRental? memoryRental,
            IPeriodicLogicRunner? reconnectableSharedLogicRunner = null)
            : base(ReconnectableInfo.TransportName, logger, memoryRental)
        {
            mUnderlyingTransportProducer = underlyingTransportProducer;
            mDisconnectTimeout = disconnectTimeout;
            _reconnectableSharedLogicRunner = reconnectableSharedLogicRunner;
        }

        protected override bool BeginConnect()
        {
            if (Handler == null)
            {
                Log.e("Handler is not set");
                return false;
            }
            
            mLogic = new ReconnectableClientLogic(mUnderlyingTransportProducer, Handler, mDisconnectTimeout);

            mLogic.OnConnected += ConnectionFinished;

            mLogic.OnStopped += (reason) => { Stop(reason); };

            var logicTickTime = DeltaTime.FromMiliseconds(20);

            if (_reconnectableSharedLogicRunner != null)
            {
                mLogicDriver = _reconnectableSharedLogicRunner.Run(mLogic, logicTickTime);
            }
            else
            {
                var threadedDriver = new PeriodicLogicThreadedDriver(logicTickTime, 128);
                threadedDriver.Start(mLogic, Log);
                mLogicDriver = threadedDriver;
            }

            return true;
        }

        protected override void OnReadyToConnect()
        {
            // TODO: Fix possible race
        }

        protected override void DestroyTransport(StopReason reason)
        {
            var logic = mLogic;
            if (logic != null)
            {
                mLogicDriver?.Stop();
            }

            mLogic = null;
        }

        public override int MessageMaxByteSize => mLogic?.MessageMaxByteSize ?? 0;

        public override string ToString()
        {
            var logic = mLogic;
            if (logic != null)
            {
                return "reconnectable-client[" + logic.SessionId + "]";
            }

            return "reconnectable-client[null]";
        }
    }
}