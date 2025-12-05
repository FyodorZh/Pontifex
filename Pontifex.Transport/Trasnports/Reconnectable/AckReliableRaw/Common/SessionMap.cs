using Actuarius.Collections;

namespace Transport.Protocols.Reconnectable.AckReliableRaw
{
    public class SessionMap<TSession>
        where TSession : class
    {
        private readonly int mCapacity;

        private readonly TinyConcurrentQueue<SessionId> mFreeSessions = new TinyConcurrentQueue<SessionId>();

        private volatile TSession[] mSessions;
        private volatile int[] mGenerations;

        private int mNextFreeId;

        public SessionMap(int capacity)
        {
            mCapacity = capacity;
            mSessions = new TSession[mCapacity];
            mGenerations = new int[mCapacity];
        }

        public SessionId AddSession(TSession session)
        {
            if (!mFreeSessions.TryPop(out var freeId))
            {
                int id = System.Threading.Interlocked.Increment(ref mNextFreeId) - 1;
                if (id >= mCapacity)
                {
                    System.Threading.Interlocked.Decrement(ref mNextFreeId);
                    return SessionId.Invalid;
                }
                freeId = new SessionId(id, 1);
            }

            mSessions[freeId.Id] = session;
            mGenerations[freeId.Id] = freeId.Generation;

            return freeId;
        }

        public bool RemoveSession(SessionId id)
        {
            if (id.Id >= 0 && id.Id < mCapacity)
            {
                int generation = System.Threading.Interlocked.CompareExchange(ref mGenerations[id.Id], 0, id.Generation);
                if (generation == id.Generation)
                {
                    mSessions[id.Id] = null!;
                    mFreeSessions.Put(new SessionId(id.Id, id.Generation + 1));
                    return true;
                }
            }
            return false;
        }

        public TSession? Find(SessionId id)
        {
            if (id.Id >= 0 && id.Id < mCapacity)
            {
                if (mGenerations[id.Id] == id.Generation)
                {
                    TSession session = mSessions[id.Id];
                    if (mGenerations[id.Id] == id.Generation)
                    {
                        return session;
                    }
                }
            }
            return null;
        }
    }
}
