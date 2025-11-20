using System;
using System.Threading;
using Actuarius.Collections;
using NUnit.Framework;
using Shared;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    class ConcurrentQueueTest
    {
        private class QueueLimiter<T> : IConcurrentQueue_old<T>
        {
            private readonly IConcurrentQueue_old<T> mQueue;

            private readonly int mCapacity;
            private int mCount;

            public QueueLimiter(IConcurrentQueue_old<T> queue, int capacity)
            {
                mQueue = queue;
                mCapacity = capacity;
            }

            public bool Put(T value)
            {
                int count = Interlocked.Increment(ref mCount);
                if (count <= mCapacity)
                {
                    return mQueue.Put(value);
                }

                Interlocked.Decrement(ref mCount);
                return false;
            }

            public bool TryPop(out T value)
            {
                if (mQueue.TryPop(out value))
                {
                    Interlocked.Decrement(ref mCount);
                    return true;
                }
                return false;
            }
        }

        private struct Pair
        {
            public int Id;
            public long Value;
        }

        private void DoTest(IConcurrentQueue_old<Pair> queue, int writeConcurrency)
        {
            long[] table = new long[writeConcurrency];

            int inProgress = writeConcurrency;
            for (int i = 0; i < writeConcurrency; ++i)
            {
                Thread th = new Thread((p) =>
                {
                    int id = (int)p;

                    for (long k = 0; k < 1e6; ++k)
                    {
                        Pair pair = new Pair() { Id = id, Value = k };
                        while (!queue.Put(pair)) ;
                    }

                    Interlocked.Decrement(ref inProgress);
                });
                th.Start(i);
            }

            DateTime t = DateTime.UtcNow;

            Pair val;
            while (inProgress > 0)
            {
                while (queue.TryPop(out val))
                {
                    Assert.True(table[val.Id] == val.Value);
                    table[val.Id] += 1;
                }
            }

            Assert.Pass("Time = " + (DateTime.UtcNow - t).TotalSeconds + "s");
        }

        private void DoTest(ISingleReaderWriterConcurrentUnorderedCollection<long> queue) // it must be queue
        {
            long lastId = 0;

            Thread th = new Thread((p) =>
            {
                for (long k = 0; k < 1e7; ++k)
                {
                    while (!queue.Put(k)) ;
                }
            });
            th.Start();

            DateTime t = DateTime.UtcNow;

            while (th.IsAlive)
            {
                long val;
                while (queue.TryPop(out val))
                {
                    Assert.True(lastId == val);
                    lastId += 1;
                }
            }

            Assert.Pass("Time = " + (DateTime.UtcNow - t).TotalSeconds + "s");
        }

        [Test]
        public void LimitedConcurrentQueueTest()
        {
            DoTest(new LimitedConcurrentQueue<Pair>(10000), 3);
        }

        [Test]
        public void TinyConcurrentQueueTest()
        {
            DoTest(new QueueLimiter<Pair>(new Shared.TinyConcurrentQueue<Pair>(), 10000), 3);
        }

        [Test]
        public void SingleReaderWriterConcurrentQueueTest()
        {
            DoTest(new SingleReaderWriterConcurrentQueue<long>());
        }
    }
}
