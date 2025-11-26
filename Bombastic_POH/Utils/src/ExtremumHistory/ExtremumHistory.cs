using System;
using System.Collections.Generic;
using Actuarius.PeriodicLogic;

namespace Shared
{
    public abstract class ExtremumHistory<TData>
    {
        private readonly System.TimeSpan mPeriod;
        private readonly IDateTimeProvider mDateTimeProvider;

        private readonly Queue<DateTime> mHistoryTime = new Queue<DateTime>();
        private readonly Queue<TData> mHistoryData = new Queue<TData>();
        private TData mExtremum;
        private int mExtremumsNumber;

        protected abstract void SetToMinValue(out TData value);
        protected abstract int Compare(TData d1, TData d2);
        protected virtual DateTime Now { get { return mDateTimeProvider.Now; } }

        protected ExtremumHistory(System.TimeSpan period, IDateTimeProvider dateTimeProvider)
        {
            mPeriod = period;
            mDateTimeProvider = dateTimeProvider;
        }

        public TData Extremum
        {
            get { return mExtremum; }
        }

        public void Push(TData value)
        {
            DateTime now = Now;

            {
                int cmp = Compare(value, mExtremum);
                if (cmp >= 0)
                {
                    mExtremum = value;
                    mExtremumsNumber = 1;
                    mHistoryTime.Clear();
                    mHistoryData.Clear();
                }

                mHistoryTime.Enqueue(now);
                mHistoryData.Enqueue(value);
            }

            while (mHistoryTime.Count > 0 && (now - mHistoryTime.Peek()) > mPeriod)
            {
                mHistoryTime.Dequeue();
                TData oldValue = mHistoryData.Dequeue();
                if (Compare(oldValue, mExtremum) == 0)
                {
                    mExtremumsNumber -= 1;
                }
            }

            if (mExtremumsNumber <= 0)
            {
                GetExtremum(mHistoryData.GetEnumerator(), out mExtremum, out mExtremumsNumber);

                while (mHistoryTime.Count > 0 && Compare(mHistoryData.Peek(), mExtremum) < 0)
                {
                    mHistoryTime.Dequeue();
                    mHistoryData.Dequeue();
                }
            }
        }

        protected virtual void GetExtremum(Queue<TData>.Enumerator enumerator, out TData extremum, out int count)
        {
            count = 0;
            SetToMinValue(out extremum);
            using (enumerator)
            {
                while (enumerator.MoveNext())
                {
                    TData element = enumerator.Current;
                    int cmp = Compare(element, extremum);
                    switch (cmp)
                    {
                        case 1:
                            extremum = element;
                            count = 1;
                            break;
                        case 0:
                            count += 1;
                            break;
                    }
                }
            }
        }
    }
}