using System.Threading;

namespace Shared.Concurrent
{
    /// <summary>
    /// Хранит данные, позволяет вычитывать и записывать их атомарно
    /// </summary>
    /// <typeparam name="TData"> Данные </typeparam>
    public class AtomicBox<TData>
        where TData : struct
    {
        private TData mValue;

        private readonly ReaderWriterLockSlim mLock = new ReaderWriterLockSlim();

        public TData Value
        {
            get
            {
                mLock.EnterReadLock();
                var res = mValue;
                mLock.ExitReadLock();
                return res;
            }
            set
            {
                mLock.EnterWriteLock();
                mValue = value;
                mLock.ExitWriteLock();
            }
        }
    }
}