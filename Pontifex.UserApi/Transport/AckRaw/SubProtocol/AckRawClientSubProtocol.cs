using Shared;
using Transport;
using Transport.Abstractions.Clients;

namespace Pontifex.UserApi
{
    public abstract class AckRawClientSubProtocol<TProtocol> : AckRawClientProtocol
        where TProtocol : Protocol, new()
    {
        private readonly string mProtocolName;

        protected AckRawClientSubProtocol(IAckRawClient transport, IModelsHashDB protocolModelHashes)
            : base(transport, protocolModelHashes, UtcNowDateTimeProvider.Instance)
        {
            mProtocolName = typeof(TProtocol).ToString();
        }

        protected abstract void WriteUserAckData(UnionDataList ackData);

        protected sealed override void WriteAckData(UnionDataList ackData)
        {
            WriteUserAckData(ackData);
            ackData.PutFirst(mProtocolName);
        }
    }
}