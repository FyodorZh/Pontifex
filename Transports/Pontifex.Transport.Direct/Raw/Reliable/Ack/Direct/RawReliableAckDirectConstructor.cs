using Pontifex.Factory;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Raw.Reliable.Direct;

namespace Pontifex.Raw.Reliable.Ack.Direct
{
    public class RawReliableAckDirectConstructor : RawReliableDirectConstructor
    {
        public override TransportType Type => TransportType.RawReliableAck;

        public override string Name => RawReliableAckDirectInfo.TransportName;

        protected override RawReliableAckClientTransport CreateClient(ITransportBuilder builder, string id)
            => new RawReliableAckDirectClient(id, builder.Logger, builder.MemoryRental);

        protected override RawReliableAckServerTransport CreateServer(ITransportBuilder builder, string id)
            => new RawReliableAckDirectServer(id, builder.Logger, builder.MemoryRental);
    }
}
