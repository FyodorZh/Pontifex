using System;
using System.Threading;
using Actuarius.Memory;
using Actuarius.Memory.Internal;
using NUnit.Framework;
using Shared;
using Shared.Pooling;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    public class ConcurrentPool
    {
        private class Collectable : MultiRefCollectableResource<Collectable>
        {
            private volatile int mState = 1;

            public int Data { get; set; }

            protected override void OnCollected()
            {
                Assert.True(System.Threading.Interlocked.Exchange(ref mState, 0) == 1);
            }

            protected override void OnRestored()
            {
                mState = 1;
            }

            protected override void OnRefCountError(ResourceUsageErrorType error, ActionHistoryTracer? tracer)
            {
                Assert.Fail(error.ToString());
                base.OnRefCountError(error, tracer);
            }

            ~Collectable()
            {
                Assert.That(mState, Is.EqualTo(0));
            }
        }

        [Test]
        public void TestCollectable()
        {
            var pool = new CollectableObjectConcurrentPool<Collectable>(10);
            Test(pool);
        }

        [Test]
        public void TestGlobalBufferedPool()
        {
            var pool = new CollectableObjectConcurrentPool<Collectable>(new SmallObjectBufferedPool<Collectable>(DefaultConstructor<Collectable>.Instance));
            Test(pool);
        }


        private void Test(IConcurrentPool<Collectable> pool)
        {
            int threadsCount = 5;
            int tableSize = 100;

            Thread[] threads = new Thread[threadsCount];
            for (int i = 0; i < threadsCount; ++i)
            {
                threads[i] = new System.Threading.Thread(
                    (pId) =>
                    {
                        int id = (int)pId;

                        Collectable[] table = new Collectable[tableSize];
                        Random rnd = new Random(id);

                        for (int tableLimit = 1; tableLimit <= tableSize; tableLimit += 3)
                            for (int j = 0; j < 1e4; ++j)
                            {
                                int k = rnd.Next(tableLimit);

                                if (table[k] == null)
                                {
                                    table[k] = pool.Acquire();
                                    table[k].Data = id;
                                }
                                else
                                {
                                    Assert.AreEqual(id, table[k].Data);
                                    table[k].Release();
                                    table[k] = null;
                                }
                            }

                        for (int j = 0; j < table.Length; ++j)
                        {
                            if (table[j] != null)
                            {
                                Assert.AreEqual(id, table[j].Data);
                                table[j].Release();
                                table[j] = null;
                            }
                        }
                    });

                threads[i].Start(i);
            }

            for (int i = 0; i < threadsCount; ++i)
            {
                threads[i].Join();
            }

            System.Threading.Thread.Sleep(100);
            System.GC.Collect(2, GCCollectionMode.Forced);
        }
    }
}