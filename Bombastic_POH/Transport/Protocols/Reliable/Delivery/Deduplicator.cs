using Shared;

namespace Transport.Protocols.Reliable.Delivery
{
    public class Deduplicator
    {
        public enum Result
        {
            New,
            Duplicate,
            Overflow
        }

        private CycleQueue<bool> mQueue;
        private uint mFrom;
        private uint mTill;

        public Deduplicator(int capacity)
        {
            mQueue = new CycleQueue<bool>(capacity, false);
            mFrom = 1;
            mTill = 0;
        }

        public Result Received(uint id)
        {
            if (mQueue.Count == 0 || id > mTill)
            {
                for (uint i = mTill + 1; i <= id; ++i)
                {
                    if (!mQueue.Put(i == id))
                    {
                        mTill = i - 1;
                        return Result.Overflow;
                    }
                }
                mTill = id;

                Trim();
                return Result.New;
            }

            // mQueue.Count > 0

            if (id < mFrom)
            {
                return Result.Duplicate;
            }

            // id in [mFrom; mTill]

            int pos = (int)(id - mFrom);
            if (mQueue[pos])
            {
                return Result.Duplicate;
            }

            mQueue[pos] = true;
            Trim();

            return Result.New;
        }

        private void Trim()
        {
            while (mTill - (int)mFrom > 0 && mQueue[0])
            {
                bool tmp;
                mQueue.TryPop(out tmp);
                mFrom += 1;
            }
        }
    }
}
