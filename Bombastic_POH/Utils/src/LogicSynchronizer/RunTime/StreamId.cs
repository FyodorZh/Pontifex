using Serializer.BinarySerializer;

namespace Shared.LogicSynchronizer
{
    public struct StreamId
    {
        public readonly ushort Id;
        public StreamId(ushort id)
        {
            Id = id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is StreamId && Equals((StreamId)obj);
        }

        public bool Equals(StreamId other)
        {
            return Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(StreamId id1, StreamId id2)
        {
            return id1.Id == id2.Id;
        }

        public static bool operator !=(StreamId id1, StreamId id2)
        {
            return id1.Id != id2.Id;
        }
    }

    public static class StreamIdExtensions
    {
        public static void AddStreamId(this IDataWriter serializer, StreamId v)
        {
            serializer.AddUInt16(v.Id);
        }
        public static StreamId ReadStreamId(this IDataReader serializer)
        {
            return new StreamId(serializer.ReadUInt16());
        }
    }
}
