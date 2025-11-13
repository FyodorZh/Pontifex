using System.Collections.Generic;
using System.Threading;

namespace Shared
{
    public class TrivialConcurrentSet<TData> : IConcurrentSet<TData>
    {
        private readonly HashSet<TData> mSet = new HashSet<TData>();
        private readonly ReaderWriterLockSlim mLocker = new ReaderWriterLockSlim();

        public bool Put(TData element)
        {
            bool bLocked = false;
            try
            {
                mLocker.EnterWriteLock();
                bLocked = true;
                return mSet.Add(element);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (bLocked)
                {
                    mLocker.ExitWriteLock();
                }
            }
        }

        public bool Remove(TData element)
        {
            bool bLocked = false;
            try
            {
                mLocker.EnterWriteLock();
                bLocked = true;
                return mSet.Remove(element);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (bLocked)
                {
                    mLocker.ExitWriteLock();
                }
            }
        }

        public bool Contains(TData element)
        {
            bool bLocked = false;
            try
            {
                mLocker.EnterReadLock();
                bLocked = true;
                return mSet.Contains(element);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (bLocked)
                {
                    mLocker.ExitReadLock();
                }
            }
        }
    }
}
