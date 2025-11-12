//#define CHECK

namespace Shared
{
    public struct RefCounter
    {
        private volatile int mRefCount;

#if CHECK
        private struct RefChangeEvent
        {
            public bool IsIncrement;
            public System.Diagnostics.StackTrace Stack;
        }

        private System.Collections.Generic.List<RefChangeEvent> mHistory;
#endif

        public void Init()
        {
            mRefCount = 1;
#if CHECK
            mHistory = new System.Collections.Generic.List<RefChangeEvent>();
            mHistory.Add(new RefChangeEvent() { IsIncrement = true, Stack = new System.Diagnostics.StackTrace() });
#endif
        }

        public bool IsValid
        {
            get { return mRefCount > 0; }
        }

        /// <summary>
        /// Увеличивает счётчик
        /// </summary>
        /// <returns> Возвращает 0 в случае неуспеха </returns>
        public int AddRef()
        {
            while (true)
            {
                int oldCount = mRefCount;
                if (oldCount <= 0)
                {
#if CHECK
                    Log.e("Wrong ref counter usage (AddRef failed):");
                    for (int i = 0; i < mHistory.Count; ++i)
                    {
                        Log.e((mHistory[i].IsIncrement ? "+" : "-") + mHistory[i]);
                    }
#endif
                    return 0;
                }

                if (System.Threading.Interlocked.CompareExchange(ref mRefCount, oldCount + 1, oldCount) == oldCount)
                {
#if CHECK
                    mHistory.Add(new RefChangeEvent() { IsIncrement = true, Stack = new System.Diagnostics.StackTrace() });
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
            int res = System.Threading.Interlocked.Decrement(ref mRefCount);
#if CHECK
            mHistory.Add(new RefChangeEvent() { IsIncrement = false, Stack = new System.Diagnostics.StackTrace() });
            if (res < 0)
            {
                Log.e("Wrong ref counter usage (Release failed):");
                for (int i = 0; i < mHistory.Count; ++i)
                {
                    Log.e((mHistory[i].IsIncrement ? "+" : "-") + mHistory[i]);
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
                int oldCount = mRefCount;
                if (oldCount != 0)
                {
                    return false;
                }

                if (System.Threading.Interlocked.CompareExchange(ref mRefCount, oldCount + 1, oldCount) == oldCount)
                {
                    return true;
                }
            }
        }
    }
}
