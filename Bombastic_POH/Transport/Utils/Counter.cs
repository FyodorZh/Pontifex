using System;
using System.Threading;
using Actuarius.Collections;
using Shared;
using Transport.Protocols.Reliable.Delivery;
using TimeSpan = System.TimeSpan;

namespace Transport.Utils
{
    internal class SumCounter
    {
        private struct Data
        {
            public readonly int Dv;
            public readonly int Dt;
            public Data(int dv, int dt)
            {
                Dv = dv;
                Dt = dt;
            }
        }

        private struct Info
        {
            public readonly int Value;
            public readonly int Count;

            public Info(int value, int count)
            {
                Value = value;
                Count = count;
            }
        }

        private long mTotal;

        private readonly int mMaxRange;
        private int mCurrentRange;
        private DateTime mLastTick = DateTime.UtcNow;

        private int mValue;

        private readonly CycleQueue<Data> mQueue = new CycleQueue<Data>();

        private readonly IDateTimeProvider mTimeProvider;

        public SumCounter(IDateTimeProvider timeProvider, TimeSpan sumRange)
        {
            mMaxRange = (int)(sumRange.TotalMilliseconds + 0.5);
            mTimeProvider = timeProvider;
        }

        public long Total
        {
            get { return mTotal; }
        }

        public double SumInRange
        {
            get
            {
                return UpdateSpeed(0).Value;
            }
        }

        public int ValuesInRange
        {
            get
            {
                return UpdateSpeed(0).Count;
            }
        }

        public void Inc(int delta)
        {
            Interlocked.Add(ref mTotal, delta);
            UpdateSpeed(delta);
        }

        private Info UpdateSpeed(int delta)
        {
            int dt = 0;
            lock (mQueue)
            {
                DateTime now = mTimeProvider.Now;
                dt = (int)((now - mLastTick).TotalMilliseconds + 0.5);
                mLastTick = now;

                mQueue.Put(new Data(delta, dt));
                mCurrentRange += dt;
                mValue += delta;

                while (mQueue.Count > 0 && mCurrentRange - mQueue.Head.Dt >= mMaxRange)
                {
                    Data data;
                    mQueue.TryPop(out data);
                    mCurrentRange -= data.Dt;
                    mValue -= data.Dv;
                }

                return new Info(mValue, mQueue.Count);
            }
        }
    }
}
