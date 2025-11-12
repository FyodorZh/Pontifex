using System.Collections.Generic;

namespace Shared
{
    public class ThreadSingletonContext
    {
        [System.ThreadStatic]
        private static ThreadSingletonContext mContext;

        private readonly List<IThreadSingleton> mSingletons = new List<IThreadSingleton>();

        public static ThreadSingletonContext ActiveInstance
        {
            get
            {
                ThreadSingletonContext context = mContext;
                if (context == null)
                {
                    context = new ThreadSingletonContext();
                    mContext = context;
                }

                return context;
            }
        }

        public IThreadSingleton TryGet(int id)
        {
            if (id < mSingletons.Count)
            {
                return mSingletons[id];
            }

            return null;
        }

        public void Register(int id, IThreadSingleton pool)
        {
            while (mSingletons.Count <= id)
            {
                mSingletons.Add(null);
            }

            mSingletons[id] = pool;
        }

        public ThreadSingletonContext Activate()
        {
            ThreadSingletonContext old = mContext;
            mContext = this;
            return old;
        }

        public void Deactivate()
        {
            mContext = null;
        }

        public List<IThreadSingleton>.Enumerator GetEnumerator()
        {
            return mSingletons.GetEnumerator();
        }

        public void Free()
        {
            int count = mSingletons.Count;
            for (int i = 0; i < count; ++i)
            {
                mSingletons[i] = null;
            }
        }
    }
}