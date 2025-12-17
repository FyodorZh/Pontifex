using System;
using Actuarius.PeriodicLogic;
using Pontifex.Abstractions.Controls;

namespace Transport.Utils
{
    public class TrafficCollectorSlim: ITrafficCollector, ITrafficCollectorSink
    {
        private readonly IDateTimeProvider mTimeProvider;

        private int mIncomingPPS;
        private int mOutgoingPPS;
        private int mIncomingTPS;
        private int mOutgoingTPS;
        private int mIncomingT;
        private int mOutgoingT;

        private int mIncomingPPSAccumulator;
        private int mOutgoingPPSAccumulator;
        private int mIncomingTPSAccumulator;
        private int mOutgoingTPSAccumulator;
        private int mIncomingTAccumulator;
        private int mOutgoingTAccumulator;

        private DateTime mStatisticsDropTime;

        private readonly object mLocker = new object();

        public string Name { get; }

        public TrafficCollectorSlim(string name, IDateTimeProvider timeProvider)
        {
            Name = name;
            mTimeProvider = timeProvider;
        }

        public void IncInTraffic(int value)
        {
            Refresh();
            System.Threading.Interlocked.Add(ref mIncomingTAccumulator, value);
            System.Threading.Interlocked.Add(ref mIncomingTPSAccumulator, value);
            System.Threading.Interlocked.Increment(ref mIncomingPPSAccumulator);
        }

        public void IncOutTraffic(int value)
        {
            Refresh();
            System.Threading.Interlocked.Add(ref mOutgoingTAccumulator, value);
            System.Threading.Interlocked.Add(ref mOutgoingTPSAccumulator, value);
            System.Threading.Interlocked.Increment(ref mOutgoingPPSAccumulator);
        }

        public long InTraffic => mIncomingT;

        public long OutTraffic => mOutgoingT;

        public int InSpeed => mIncomingTPS;

        public int OutSpeed => mOutgoingTPS;

        public int InPacketsSpeed => mIncomingPPS;

        public int OutPacketsSpeed => mOutgoingPPS;

        private void Refresh()
        {
            var now = mTimeProvider.Now;
            if (now > mStatisticsDropTime)
            {
                lock (mLocker)
                {
                    if (now > mStatisticsDropTime)
                    {
                        mStatisticsDropTime = now.AddSeconds(1);

                        mIncomingPPS = mIncomingPPSAccumulator;
                        mIncomingTPS = mIncomingTPSAccumulator;
                        mOutgoingPPS = mOutgoingPPSAccumulator;
                        mOutgoingTPS = mOutgoingTPSAccumulator;
                        mIncomingT = mIncomingTAccumulator;
                        mOutgoingT = mOutgoingTAccumulator;

                        mIncomingPPSAccumulator = 0;
                        mIncomingTPSAccumulator = 0;
                        mOutgoingPPSAccumulator = 0;
                        mOutgoingTPSAccumulator = 0;
                        mIncomingTAccumulator = 0;
                        mOutgoingTAccumulator = 0;
                    }
                }
            }
        }
    }
}