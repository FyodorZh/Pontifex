using System;
using System.Collections.Generic;
using Fundamentum.Collections;

namespace Shared
{
    /// <summary>
    /// Ассоциативный массив элементов с автоматическим удалением элементов по таймауту
    /// </summary>
    public class TemporaryMap<TKey, TData> : IMap<TKey, TData> // TODO: UnitTest
    {
        private readonly Dictionary<TKey, TData> mTable = new Dictionary<TKey, TData>();
        private readonly Dictionary<TKey, bool> mFlags = new Dictionary<TKey, bool>();

        private readonly CycleQueue<DateTime> mListOfTimes = new CycleQueue<DateTime>();
        private readonly CycleQueue<TKey> mListOfKeys = new CycleQueue<TKey>();

        private readonly IDateTimeProvider mTimeProvider;
        private readonly System.TimeSpan mExpirationTime;

        /// <summary>
        /// </summary>
        /// <param name="timeProvider"> Источник времени </param>
        /// <param name="expirationTime"> Период через который элементы протухаю если к ним не было обращений </param>
        public TemporaryMap(IDateTimeProvider timeProvider, System.TimeSpan expirationTime)
        {
            mTimeProvider = timeProvider;
            mExpirationTime = expirationTime;
        }

        public bool Add(TKey key, TData element)
        {
            try
            {
                mTable.Add(key, element);
                mFlags.Add(key, false);

                mListOfKeys.Put(key);
                mListOfTimes.Put(mTimeProvider.Now);

                return true;
            }
            catch (Exception e)
            {
                Log.wtf(e);
                return false;
            }
        }

        public bool Remove(TKey key)
        {
            bool res = mTable.Remove(key);
            if (res)
            {
                mFlags.Remove(key);
            }
            Refresh();
            return res;
        }

        /// <summary>
        /// Если элемент нашёлся, то для него сбрасывается период протухания
        /// </summary>if (!mEPointsMap.TryGetValue(sender, out ep))
        /// <returns></returns>
        public bool TryGetValue(TKey key, out TData element)
        {
            bool res = mTable.TryGetValue(key, out element);
            if (res)
            {
                mFlags[key] = true;
            }
            Refresh();
            return res;
        }

        private void Refresh()
        {
            var now = mTimeProvider.Now;
            while (mListOfTimes.Count > 0 && mListOfTimes.Head >= now)
            {
                DateTime timeToRemove;
                TKey keyToRemove;

                mListOfTimes.TryPop(out timeToRemove);
                mListOfKeys.TryPop(out keyToRemove);

                bool reload;
                if (mFlags.TryGetValue(keyToRemove, out reload))
                {
                    if (reload)
                    {
                        mListOfKeys.Put(keyToRemove);
                        mListOfTimes.Put(now.Add(mExpirationTime));
                        continue;
                    }
                }

                mFlags.Remove(keyToRemove);
                mTable.Remove(keyToRemove);
            }
        }
    }
}