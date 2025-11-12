using System;
using NewProtocol;
using Shared;
using Transport.Abstractions.Servers;

namespace TransportStressTest
{
    class StressTestProtocolFactory : AckRawServerProtocolFactoryBase
    {
        private static readonly byte[] mAck = {1, 2, 3};

        public StressTestProtocolFactory(IModelsHashDB protocolModelHashes, IAckRawServer transport, Action<int> onTickDelay = null)
            : base(protocolModelHashes, transport, onTickDelay)
        {
        }

        protected override AckRawServerProtocol ConstructSSP(ByteArraySegment ackData, ILogger logger)
        {
            if (ackData.EqualByContent(new ByteArraySegment(mAck)))
            {
                return new ServerSideStressTest();
            }

            return null;
        }
    }
}