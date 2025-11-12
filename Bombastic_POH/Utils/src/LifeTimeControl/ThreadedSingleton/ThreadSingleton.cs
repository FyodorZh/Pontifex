namespace Shared
{
    public abstract class ThreadSingleton<TSelf> : IThreadSingleton
        where TSelf : ThreadSingleton<TSelf>, new()
    {
        private static readonly int mId = Internal.ThreadSingletonIdsManager.GenNew();

        private static TSelf TryGetInstance()
        {
            var context = ThreadSingletonContext.ActiveInstance;
            return context.TryGet(mId) as TSelf;
        }

        private static void SetInstance(TSelf instance)
        {
            var context = ThreadSingletonContext.ActiveInstance;
            context.Register(mId, instance);
        }

        public static TSelf Instance
        {
            get
            {
                TSelf self = TryGetInstance();
                if (self == null)
                {
                    self = new TSelf();
                    SetInstance(self);
                }

                return self;
            }
        }
    }
}