using System;
using System.Collections.Generic;
using System.Threading;

namespace Shared
{
    public class TrivialConcurrentDictionary<TKey, TData> : IConcurrentMap<TKey, TData>
        where TKey : IEquatable<TKey>
    {
        private readonly Dictionary<TKey, TData> mDictionary = new Dictionary<TKey, TData>();
        private readonly ReaderWriterLockSlim mLocker = new ReaderWriterLockSlim();

        public bool Add(TKey key, TData element)
        {
            bool bLocked = false;
            try
            {
                mLocker.EnterWriteLock();
                bLocked = true;
                mDictionary.Add(key, element);
                return true;
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

        public bool Remove(TKey key)
        {
            bool bLocked = false;
            try
            {
                mLocker.EnterWriteLock();
                bLocked = true;
                return mDictionary.Remove(key);
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

        public bool TryGetValue(TKey key, out TData element)
        {
            bool bLocked = false;
            try
            {
                mLocker.EnterReadLock();
                bLocked = true;
                return mDictionary.TryGetValue(key, out element);
            }
            catch
            {
                element = default(TData);
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