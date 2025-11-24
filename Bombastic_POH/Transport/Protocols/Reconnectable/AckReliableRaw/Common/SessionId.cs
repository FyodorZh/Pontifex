namespace Transport.Protocols.Reconnectable.AckReliableRaw
{
    public struct SessionId : System.IEquatable<SessionId>
    {
        public static readonly SessionId Invalid = new SessionId(-1, 0);

        private readonly int mId;
        private readonly int mGeneration;

        public SessionId(int id, int generation)
        {
            mId = id;
            mGeneration = generation;
        }

        public bool IsValid
        {
            get { return mId >= 0; }
        }

        public int Id
        {
            get { return mId; }
        }

        public int Generation
        {
            get { return mGeneration; }
        }

        public bool Equals(SessionId other)
        {
            return mId == other.mId && mGeneration == other.mGeneration;
        }

        public override string ToString()
        {
            return string.Format("[{0}.{1}]", mId, mGeneration);
        }

        public byte[] Serialize()
        {
            BufferElement id = new BufferElement(mId);
            BufferElement generation = new BufferElement(mGeneration);

            byte[] data = new byte[1 + id.Size + generation.Size];
            data[0] = (byte)data.Length;

            var sink = ByteArraySink.ThreadInstance(data, 1);

            id.TryWriteTo(sink);
            generation.TryWriteTo(sink);

            return data;
        }

        public static bool TryDeserialize(out SessionId sessionId, ByteArraySegment data)
        {
            sessionId = SessionId.Invalid;
            if (data.Count < 1)
            {
                return false;
            }

            if (data.Count != data[0])
            {
            }

            int pos = data.Offset + 1;

            int id;
            int generation;
            if (!new BufferElement(data.ReadOnlyArray, ref pos, null).AsInt32(out id) ||
                !new BufferElement(data.ReadOnlyArray, ref pos, null).AsInt32(out generation))
            {
                return false;
            }

            sessionId = new SessionId(id, generation);
            return true;
        }
    }
}