using System;
using Shared;
using Shared.Utils;
using Transport.Abstractions.Clients;
using Transport.Transports.Core;

namespace Transport.Protocols.Reconnectable.AckReliableRaw
{
    public sealed class AckRawReconnectableClient : AckRawClient, IAckReliableRawClient
    {
        private readonly Func<IAckReliableRawClient> mUnderlyingTransportProducer;
        private readonly System.TimeSpan mDisconnectTimeout;
        private readonly IPeriodicLogicRunner _reconnectableSharedLogicRunner;

        private ReconnectableClientLogic mLogic;
        private ILogicDriverCtl mLogicDriver;

        public AckRawReconnectableClient(
            Func<IAckReliableRawClient> underlyingTransportProducer,
            System.TimeSpan disconnectTimeout,
            IPeriodicLogicRunner reconnectableSharedLogicRunner = null)
            : base(ReconnectableInfo.TransportName)
        {
            mUnderlyingTransportProducer = underlyingTransportProducer;
            mDisconnectTimeout = disconnectTimeout;
            _reconnectableSharedLogicRunner = reconnectableSharedLogicRunner;
        }

        protected override bool BeginConnect()
        {
            if (mUnderlyingTransportProducer != null)
            {
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

            return false;
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
                mLogicDriver.Stop();
            }

            mLogic = null;
        }

        public override int MessageMaxByteSize
        {
            get
            {
                var logic = mLogic;
                return logic != null ? logic.MessageMaxByteSize : 0;
            }
        }

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