using Pontifex.Factory;
using Pontifex.Raw.Unreliable.Direct;

namespace Pontifex.Raw.Unreliable.Ack.Direct
{
    public class RawUnreliableAckDirectConstructor : RawUnreliableDirectConstructor
    {
        public override TransportType Type => TransportType.RawUnreliableAck;

        public override string Name => RawUnreliableAckDirectInfo.TransportName;

        protected override RawUnreliableDirectClientTransport CreateClient(ITransportBuilder builder, string id)
            => new RawUnreliableAckDirectClient(id, builder.Logger, builder.MemoryRental);

        protected override IRawUnreliableTransport CreateServer(ITransportBuilder builder, string id)
            => new RawUnreliableAckDirectServer(id, builder.Logger, builder.MemoryRental);
    }
}
