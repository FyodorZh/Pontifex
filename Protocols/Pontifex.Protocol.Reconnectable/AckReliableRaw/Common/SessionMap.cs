using Actuarius.Collections;

namespace Pontifex.Protocols.Reconnectable.AckReliableRaw
{
    public class SessionMap<TSession>
        where TSession : class
    {
        private readonly int _capacity;

        private readonly TinyConcurrentQueue<SessionId> _freeSessions = new TinyConcurrentQueue<SessionId>();

        private volatile TSession[] _sessions;
        private volatile int[] _generations;

        private int _nextFreeId;

        public SessionMap(int capacity)
        {
            _capacity = capacity;
            _sessions = new TSession[_capacity];
            _generations = new int[_capacity];
        }

        public SessionId AddSession(TSession session)
        {
            if (!_freeSessions.TryPop(out var freeId))
            {
                int id = System.Threading.Interlocked.Increment(ref _nextFreeId) - 1;
                if (id >= _capacity)
                {
                    System.Threading.Interlocked.Decrement(ref _nextFreeId);
                    return SessionId.Invalid;
                }
                freeId = new SessionId(id, 1);
            }

            _sessions[freeId.Id] = session;
            _generations[freeId.Id] = freeId.Generation;

            return freeId;
        }

        public bool RemoveSession(SessionId id)
        {
            if (id.Id >= 0 && id.Id < _capacity)
            {
                int generation = System.Threading.Interlocked.CompareExchange(ref _generations[id.Id], 0, id.Generation);
                if (generation == id.Generation)
                {
                    _sessions[id.Id] = null!;
                    _freeSessions.Put(new SessionId(id.Id, id.Generation + 1));
                    return true;
                }
            }
            return false;
        }

        public TSession? Find(SessionId id)
        {
            if (id.Id >= 0 && id.Id < _capacity)
            {
                if (_generations[id.Id] == id.Generation)
                {
                    TSession session = _sessions[id.Id];
                    if (_generations[id.Id] == id.Generation)
                    {
                        return session;
                    }
                }
            }
            return null;
        }
    }
}
