using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Operarius;
using Scriba;

namespace Actuarius.Collections
{
    /// <summary>
    /// Ассоциативный массив элементов с автоматическим удалением элементов по таймауту
    /// </summary>
    public class TemporaryMap<TKey, TData> : IMap<TKey, TData> // TODO: UnitTest
    {
        private readonly Dictionary<TKey, TData> _table = new Dictionary<TKey, TData>();
        private readonly Dictionary<TKey, bool> _flags = new Dictionary<TKey, bool>();

        private readonly CycleQueue<DateTime> _listOfTimes = new CycleQueue<DateTime>();
        private readonly CycleQueue<TKey> _listOfKeys = new CycleQueue<TKey>();

        private readonly IDateTimeProvider _timeProvider;
        private readonly TimeSpan _expirationTime;

        /// <summary>
        /// </summary>
        /// <param name="timeProvider"> Источник времени </param>
        /// <param name="expirationTime"> Период через который элементы протухаю если к ним не было обращений </param>
        public TemporaryMap(IDateTimeProvider timeProvider, TimeSpan expirationTime)
        {
            _timeProvider = timeProvider;
            _expirationTime = expirationTime;
        }

        public bool Add(TKey key, TData element)
        {
            try
            {
                _table.Add(key, element);
                _flags.Add(key, false);

                _listOfKeys.Put(key);
                _listOfTimes.Put(_timeProvider.Now);

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
            bool res = _table.Remove(key);
            if (res)
            {
                _flags.Remove(key);
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
            bool res = _table.TryGetValue(key, out element);
            if (res)
            {
                _flags[key] = true;
            }
            Refresh();
            return res;
        }

        private void Refresh()
        {
            var now = _timeProvider.Now;
            while (_listOfTimes.Count > 0 && _listOfTimes.Head >= now)
            {
                if (_listOfTimes.TryPop(out _) && _listOfKeys.TryPop(out var keyToRemove))
                {
                    if (_flags.TryGetValue(keyToRemove, out var reload))
                    {
                        if (reload)
                        {
                            _listOfKeys.Put(keyToRemove);
                            _listOfTimes.Put(now.Add(_expirationTime));
                            continue;
                        }
                    }

                    _flags.Remove(keyToRemove);
                    _table.Remove(keyToRemove);
                }
            }
        }
    }
}