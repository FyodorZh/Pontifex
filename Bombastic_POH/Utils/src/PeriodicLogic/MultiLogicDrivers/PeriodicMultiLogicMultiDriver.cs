namespace Shared.Utils
{
    public class PeriodicMultiLogicMultiDriver : IPeriodicMultiLogicDriver
    {
        private readonly PeriodicMultiLogicDriver[] mBuckets;
        private readonly DeltaTime mPeriod;

        public PeriodicMultiLogicMultiDriver(IPeriodicLogicDriver[] drivers)
        {
            if (drivers == null || drivers.Length == 0)
            {
                throw new System.InvalidOperationException("Drivers list is empty");
            }

            mPeriod = drivers[0].Period;
            for (int i = 1; i < drivers.Length; ++i)
            {
                if (mPeriod != drivers[i].Period)
                {
                    throw new System.InvalidOperationException("Drivers have different periods");
                }
            }

            mBuckets = new PeriodicMultiLogicDriver[drivers.Length];
            for (int i = 0; i < drivers.Length; ++i)
            {
                mBuckets[i] = new PeriodicMultiLogicDriver(drivers[i]);
            }
        }

        public DeltaTime Period
        {
            get { return mPeriod; }
        }

        public int Count
        {
            get
            {
                int count = 0;
                if (mBuckets != null)
                {
                    for (int i = 0; i < mBuckets.Length; ++i)
                    {
                        count += mBuckets[i].Count;
                    }
                }
                return count;
            }
        }

        public bool Start(ILogger logger)
        {
            if (mBuckets == null)
            {
                return false;
            }

            for (int i = 0; i < mBuckets.Length; ++i)
            {
                if (!mBuckets[i].Start(logger))
                {
                    for (int j = 0; j < i; ++j)
                    {
                        mBuckets[j].Stop();
                    }
                    return false;
                }
            }
            return true;
        }

        public ILogicDriverCtl Append(IPeriodicLogic logic, DeltaTime period)
        {
            if (mBuckets != null)
            {
                int minId = -1;
                for (int i = 0; i < mBuckets.Length; ++i)
                {
                    if (minId == -1 || mBuckets[minId].Count > mBuckets[i].Count)
                    {
                        minId = i;
                    }
                }
                if (minId != -1)
                {
                    return mBuckets[minId].Append(logic, period);
                }
            }
            return null;
        }

        public void Stop()
        {
            if (mBuckets != null)
            {
                for (int i = 0; i < mBuckets.Length; ++i)
                {
                    mBuckets[i].Stop();
                }
            }
        }
    }
}
