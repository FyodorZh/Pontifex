using Shared;
using Transport;
using Transport.Abstractions.Clients;

namespace NewProtocol
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

        protected abstract byte[] GetUserAckData();

        protected sealed override byte[] GetAckData()
        {
            return AckUtils.AppendPrefix(GetUserAckData(), mProtocolName);
        }
    }
}