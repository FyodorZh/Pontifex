using Pontifex.Factory;
using Pontifex.Raw.Unreliable.Direct;

namespace Pontifex.Raw.Unreliable.NoAck.Direct
{
    public class RawUnreliableNoAckDirectConstructor : RawUnreliableDirectConstructor
    {
        public override TransportType Type => TransportType.RawUnreliableNoAck;

        public override string Name => RawUnreliableNoAckDirectInfo.TransportName;

        protected override RawUnreliableDirectClientTransport CreateClient(ITransportBuilder builder, string id)
            => new RawUnreliableNoAckDirectClient(id, builder.Logger, builder.MemoryRental);

        protected override IRawUnreliableTransport CreateServer(ITransportBuilder builder, string id)
            => new RawUnreliableNoAckDirectServer(id, builder.Logger, builder.MemoryRental);
    }
}
