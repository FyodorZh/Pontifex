using Actuarius.Memory;

namespace Pontifex.DeliveryManager
{
    internal readonly struct ParsedUserMessage
    {
        public byte Type { get; }
        public DeliveryId Id { get; }
        public byte PartId { get; }
        public byte PartsNumber { get; }
        public bool IsMultiChunk { get; }
        public IMultiRefReadOnlyByteArray Payload { get; }

        public ParsedUserMessage(
            byte type,
            DeliveryId id,
            byte partId,
            byte partsNumber,
            bool isMultiChunk,
            IMultiRefReadOnlyByteArray payload)
        {
            Type = type;
            Id = id;
            PartId = partId;
            PartsNumber = partsNumber;
            IsMultiChunk = isMultiChunk;
            Payload = payload;
        }
    }
}
