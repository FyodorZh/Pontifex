using System;
using System.Collections.Generic;
using System.Text;
using Shared;
using Transport;
using Transport.Abstractions.Servers;

namespace NewProtocol
{
    public class AckRawServerProtocolMultiFactory : AckRawServerProtocolFactoryBase
    {
        private class SubFactory
        {
            public string Hash { get; private set; }
            public string Name { get; private set; }
            public byte[] NameBytes { get; private set; }
            public IAckRawServerProtocolSubFactory Instance { get; private set; }
            
            public SubFactory(IAckRawServerProtocolSubFactory factory, IModelsHashDB protocolModelHashes)
            {
                Hash = factory.CalculateHash(protocolModelHashes);
                Name = factory.ProtocolName;
                NameBytes = Encoding.UTF8.GetBytes(Name);
                Instance = factory;
            }
        }

        private readonly IModelsHashDB mHashModels;

        private readonly List<SubFactory> mSubFactories = new List<SubFactory>();

        private bool mStarted;

        public AckRawServerProtocolMultiFactory(IModelsHashDB protocolModelHashes, IAckRawServer transport, Action<int> onTickDelay)
            : base(protocolModelHashes, transport, onTickDelay)
        {
            mHashModels = protocolModelHashes;
        }

        public bool Register(IAckRawServerProtocolSubFactory subProtocolFactory)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (!mStarted)
            {
                lock (mSubFactories)
                {
                    if (!mStarted)
                    {
                        string name = subProtocolFactory.ProtocolName;
                        for (int i = 0; i < mSubFactories.Count; ++i)
                        {
                            if (mSubFactories[i].Name == name)
                            {
                                return false;
                            }
                        }
                        mSubFactories.Add(new SubFactory(subProtocolFactory, mHashModels));
                        return true;
                    }
                }
            }
            return false;
        }

        public override bool Start(ILogger logger, DeltaTime tickPeriod, int threadPoolSize = 1)
        {
            lock (mSubFactories)
            {
                mStarted = true;
            }

            if (base.Start(logger, tickPeriod, threadPoolSize))
            {
                foreach (var factory in mSubFactories)
                {
                    logger.i("Started ServerProtocolSubFactory[{0}]. Hash = '{1}'", factory.Name, factory.Hash);
                }
                return true;
            }

            return false;
        }

        protected override AckRawServerProtocol ConstructSSP(ByteArraySegment ackData, ILogger logger)
        {
            foreach (var factory in mSubFactories)
            {
                byte[] name = factory.NameBytes;
                ByteArraySegment ack = AckUtils.CheckPrefix(ackData, name);
                if (ack.IsValid)
                {
                    return factory.Instance.ConstructSSP(ack, logger);
                }
            }
            logger.w("Unknown sub protocol request");
            return null;
        }
    }
}
