#define CHECK

using System.Collections.Generic;

namespace Shared
{
    public class RefCounter
    {
        private volatile int _refCount;

#if CHECK
        private struct RefChangeEvent
        {
            public bool IsIncrement;
            public System.Diagnostics.StackTrace Stack;
        }

        private readonly List<RefChangeEvent> _history = new();
#endif

        public void Init()
        {
            _refCount = 1;
#if CHECK
            _history.Add(new RefChangeEvent() { IsIncrement = true, Stack = new System.Diagnostics.StackTrace() });
#endif
        }

        public bool IsValid => _refCount > 0;

        /// <summary>
        /// Увеличивает счётчик
        /// </summary>
        /// <returns> Возвращает 0 в случае неуспеха </returns>
        public int AddRef()
        {
            while (true)
            {
                int oldCount = _refCount;
                if (oldCount <= 0)
                {
#if CHECK
                    Log.e("Wrong ref counter usage (AddRef failed):");
                    for (int i = 0; i < _history.Count; ++i)
                    {
                        Log.e((_history[i].IsIncrement ? "+" : "-") + _history[i]);
                    }
#endif
                    return 0;
                }

                if (System.Threading.Interlocked.CompareExchange(ref _refCount, oldCount + 1, oldCount) == oldCount)
                {
#if CHECK
                    _history.Add(new RefChangeEvent() { IsIncrement = true, Stack = new System.Diagnostics.StackTrace() });
#endif
                    return oldCount + 1;
                }
            }
        }

        /// <summary>
        /// Уменьшает счётчик
        /// </summary>
        /// <returns> Возвращает 0 когда счётчик обнулился </returns>
        public int Release()
        {
            int res = System.Threading.Interlocked.Decrement(ref _refCount);
#if CHECK
            _history.Add(new RefChangeEvent() { IsIncrement = false, Stack = new System.Diagnostics.StackTrace() });
            if (res < 0)
            {
                Log.e("Wrong ref counter usage (Release failed):");
                for (int i = 0; i < _history.Count; ++i)
                {
                    Log.e((_history[i].IsIncrement ? "+" : "-") + _history[i]);
                }
            }
#endif
            return res;
        }

        /// <summary>
        /// Восстанавливает значение счётчика в 1 если он был 0
        /// </summary>
        /// <returns></returns>
        public bool Revive()
        {
            while (true)
            {
                int oldCount = _refCount;
                if (oldCount != 0)
                {
                    return false;
                }

                if (System.Threading.Interlocked.CompareExchange(ref _refCount, oldCount + 1, oldCount) == oldCount)
                {
                    return true;
                }
            }
        }
    }
}
