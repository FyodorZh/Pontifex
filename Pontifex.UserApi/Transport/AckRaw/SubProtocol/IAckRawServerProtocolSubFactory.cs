using Shared;

namespace NewProtocol
{
    public interface IAckRawServerProtocolSubFactory
    {
        string ProtocolName { get; }
        string CalculateHash(IModelsHashDB protocolModelHashes);
        AckRawServerProtocol ConstructSSP(UnionDataList ackData, ILogger logger);
    }

    public abstract class AckRawServerProtocolSubFactory<TProtocol> : IAckRawServerProtocolSubFactory
            where TProtocol : Protocol, new()
    {
        public virtual string ProtocolName
        {
            get { return typeof(TProtocol).ToString(); }
        }

        public string CalculateHash(IModelsHashDB protocolModelHashes)
        {
            return Protocol.GetProtocolHash<TProtocol>(protocolModelHashes);
        }

        public abstract AckRawServerProtocol ConstructSSP(UnionDataList ackData, ILogger logger);
    }
}