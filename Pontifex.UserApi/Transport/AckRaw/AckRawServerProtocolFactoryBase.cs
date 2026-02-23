using System;
using Shared;
using Shared.Utils;
using Transport;
using Transport.Abstractions.Acknowledgers;
using Transport.Abstractions.Handlers.Server;
using Transport.Abstractions.Servers;

namespace Pontifex.UserApi
{
    public abstract class AckRawServerProtocolFactoryBase : IRawServerAcknowledger<IAckRawServerHandler>, IDisposable
    {
        private readonly IAckRawServer mTransport;
        private readonly Action<int> mOnTickDelay;

        private PeriodicMultiLogicMultiDriver mMultiLogic;

        private readonly IModelsHashDB mHashModels;

        protected abstract AckRawServerProtocol ConstructSSP(UnionDataList ackData, ILogger logger);

        protected AckRawServerProtocolFactoryBase(IModelsHashDB protocolModelHashes, IAckRawServer transport, Action<int> onTickDelay = null)
        {
            mHashModels = protocolModelHashes;
            mTransport = transport;
            mOnTickDelay = onTickDelay;
            mTransport.Init(this);
        }

        public virtual bool Start(ILogger logger, DeltaTime tickPeriod, int threadPoolSize = 1)
        {
            if (threadPoolSize < 1)
            {
                return false;
            }

            IPeriodicLogicDriver[] drivers = new IPeriodicLogicDriver[threadPoolSize];
            for (int i = 0; i < threadPoolSize; ++i)
            {
                drivers[i] = new PeriodicLogicThreadedDriver(tickPeriod, 128, mOnTickDelay);
            }

            mMultiLogic = new PeriodicMultiLogicMultiDriver(drivers);
            mMultiLogic.Start(logger);

            return mTransport.Start(r => { }, logger);
        }

        IAckRawServerHandler IRawServerAcknowledger<IAckRawServerHandler>.TryAck(UnionDataList ackData, ILogger logger)
        {
            var ssp = ConstructSSP(ackData, logger);
            if (ssp != null)
            {
                ((IAnyProtocolCtl)ssp).SetProtocolHashes(mHashModels);
                if (mMultiLogic.Append(ssp, mMultiLogic.Period) == null)
                {
                    Log.e("AckRawServerProtocolFactory is not ready for ACK");
                    ssp = null;
                }
            }
            return ssp;
        }

        #region IDisposable
        private bool disposedValue = false; // To detect redundant calls
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }
                if (mMultiLogic != null)
                {
                    mMultiLogic.Stop();
                    mMultiLogic = null;
                }

                mTransport.Stop(new Transport.StopReasons.UserIntention(mTransport.Type));
                disposedValue = true;
            }
        }

        ~AckRawServerProtocolFactoryBase()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
