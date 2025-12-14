namespace Transport.Protocols.Reconnectable.AckReliableRaw
{
    public readonly struct SessionId : System.IEquatable<SessionId>
    {
        public static readonly SessionId Invalid = new SessionId(-1, 0);

        private readonly int mId;
        private readonly int mGeneration;

        public SessionId(int id, int generation)
        {
            mId = id;
            mGeneration = generation;
        }

        public bool IsValid => mId >= 0;

        public int Id => mId;

        public int Generation => mGeneration;

        public bool Equals(SessionId other)
        {
            return mId == other.mId && mGeneration == other.mGeneration;
        }

        public override string ToString()
        {
            return $"[{mId}.{mGeneration}]";
        }
    }
}