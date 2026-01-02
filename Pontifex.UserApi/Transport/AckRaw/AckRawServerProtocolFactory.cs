using System;
using Shared;
using Transport.Abstractions.Servers;

namespace NewProtocol
{
    public abstract class AckRawServerProtocolFactory<TProtocol> : AckRawServerProtocolFactoryBase
        where TProtocol : Protocol, new()
    {
        private readonly IModelsHashDB mHashModels;

        protected AckRawServerProtocolFactory(IModelsHashDB protocolModelHashes, IAckRawServer transport, Action<int> onTickDelay = null)
            : base(protocolModelHashes, transport, onTickDelay)
        {
            mHashModels = protocolModelHashes;
        }

        public override bool Start(ILogger logger, DeltaTime tickPeriod, int threadPoolSize = 1)
        {
            if (base.Start(logger, tickPeriod, threadPoolSize))
            {
                var protocolHash = Protocol.GetProtocolInfo<TProtocol>(mHashModels);
                logger.i("Started ServerProtocolFactory[{0}]. Hash = '{1}'", GetType().Name, protocolHash);
                return true;
            }
            return false;
        }
    }
}
