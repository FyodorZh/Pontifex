namespace Shared.Internal
{
    internal static class ThreadSingletonIdsManager
    {
        private static int mLastId = 0;

        public static int GenNew()
        {
            return System.Threading.Interlocked.Increment(ref mLastId);
        }
    }
}