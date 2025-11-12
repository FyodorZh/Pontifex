using System;
using System.Collections.Generic;
using Shared;
using Transport.Abstractions.Controls;
using TimeSpan = System.TimeSpan;

namespace Transport.Protocols.Reliable.AckRaw
{
    /// <summary>
    /// GetPing() Вызывается ассинхронно
    /// Прочие методы должны вызываться синхронно
    /// </summary>
    internal class PingCollector : IPingCollector
    {
        private readonly Dictionary<DeliveryId, DateTime> mRequestEmitTime = new Dictionary<DeliveryId, DateTime>();
        private readonly IntMaxHistory mMaxPingHistory = new IntMaxHistory(TimeSpan.FromSeconds(1));
        private readonly IntMinHistory mMinPingHistory = new IntMinHistory(TimeSpan.FromSeconds(1));
        private volatile int mMaxPing = 0;
        private volatile int mMinPing = 0;

        public string Name { get; private set; }

        public bool CollectPing { get; set; }

        public PingCollector(string name)
        {
            Name = name;
            CollectPing = true;
        }

        public bool GetPing(out int minPing, out int maxPing, out int avgPing)
        {
            minPing = mMinPing;
            maxPing = mMaxPing;
            avgPing = (minPing + maxPing) / 2; // todo
            return CollectPing;
        }

        public void DeliveryStarted(DeliveryId id)
        {
            if (CollectPing)
            {
                if (!mRequestEmitTime.ContainsKey(id))
                {
                    mRequestEmitTime.Add(id, DateTime.UtcNow);
                }
            }
        }

        public void DeliveryFinished(DeliveryId id)
        {
            DateTime time;
            if (mRequestEmitTime.TryGetValue(id, out time))
            {
                int ping = (int)(DateTime.UtcNow - time).TotalMilliseconds;

                mMaxPingHistory.Push(ping);
                mMinPingHistory.Push(ping);

                mRequestEmitTime.Remove(id);
            }
        }

        public void Refresh()
        {
            mMinPing = mMinPingHistory.Extremum;
            mMaxPing = mMaxPingHistory.Extremum;
        }
    }
}