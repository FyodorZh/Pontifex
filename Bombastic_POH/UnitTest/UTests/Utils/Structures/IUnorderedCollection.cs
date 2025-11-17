using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Collections;
using NUnit.Framework;
using Shared;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    public class IUnorderedCollection
    {
        [Test]
        public void TestCycleQueue()
        {
            Test(new CycleQueue<int>(10), (int)1e6, 0.5001, id => ++id);
        }

        [Test]
        public void TestQueueBasedConcurrentUnorderedCollection()
        {
            Test(new QueueBasedConcurrentUnorderedCollection<int>(10), (int)1e6, 0.5001, id => ++id);
        }

        [Test]
        public void TestTinyConcurrentQueue()
        {
            Test(new TinyConcurrentQueue<int>(), (int)1e6, 0.5001, id => ++id);
        }

        [Test]
        public void TestLimitedConcurrentQueue()
        {
            Test(new LimitedConcurrentQueue<int>(10000), (int)1e6, 0.5001, id => ++id);
        }

        [Test]
        public void TestPriorityQueue()
        {
            Test(new Shared.PriorityQueue<int, int>(), (int)1e6, 0.5001, kv => new KeyValuePair<int, int>((kv.Key * 31 + 371) % 10001, kv.Value + 1));
        }

        private void Test<TElement>(IStream<TElement> collection, int testSize, double percentOfAddition, Func<TElement, TElement> nextElement)
        {
            TElement prevElement = default(TElement);

            Random rnd = new Random();

            HashSet<TElement> table = new HashSet<TElement>();

            TElement toRemove;
            for (int i = 0; i < testSize; ++i)
            {
                if (rnd.NextDouble() < percentOfAddition)
                {
                    TElement next = nextElement(prevElement);
                    prevElement = next;
                    Assert.True(collection.Put(next));
                    Assert.True(table.Add(next));
                }
                else
                {
                    if (collection.TryPop(out toRemove))
                    {
                        Assert.True(table.Remove(toRemove));
                    }
                    else
                    {
//                        if (table.Count != 0)
//                        {
//                            collection.TryDequeue(out toRemove);
//                        }
                        Assert.AreEqual(0, table.Count);
                    }
                }
            }

            Console.WriteLine("Final collection size is " + table.Count);
            while (collection.TryPop(out toRemove))
            {
                Assert.True(table.Remove(toRemove));
            }

            Assert.AreEqual(0, table.Count);
        }
    }

    [TestFixture]
    public class IConcurrentUnorderedCollection
    {
        [Test]
        public void TestTinyConcurrentQueue()
        {
            Test1(new TinyConcurrentQueue<int>(), (int)1e5, 0.5001);
            Test2(new TinyConcurrentQueue<int>(), 10, 100, 5.0);
        }

        [Test]
        public void TestLimitedConcurrentQueue()
        {
            Test1(new LimitedConcurrentQueue<int>(10000), (int)1e5, 0.5001);
            Test2(new LimitedConcurrentQueue<int>(10 * 100), 10, 100, 5.0);
        }

        [Test]
        public void TestQueueBasedConcurrentUnorderedCollection()
        {
            Test1(new QueueBasedConcurrentUnorderedCollection<int>(10), (int)1e6, 0.5001);
            Test2(new QueueBasedConcurrentUnorderedCollection<int>(10), 10, 100, 5.0);
        }

        private void Test1(IConcurrentUnorderedCollection<int> collection, int testSize, double percentOfAddition)
        {
            int prevElement = 0;

            ConcurrentDictionary<int, int> table = new ConcurrentDictionary<int, int>();

            Thread[] threads = new Thread[10];
            for (int k = 0; k < threads.Length; ++k)
            {
                threads[k] = new Thread(() =>
                {
                    Random rnd = new Random();

                    for (int i = 0; i < testSize; ++i)
                    {
                        if (rnd.NextDouble() < percentOfAddition)
                        {
                            int next = Interlocked.Increment(ref prevElement);
                            Assert.True(table.TryAdd(next, 0), "Failed to add");
                            Assert.True(collection.Put(next));
                        }
                        else
                        {
                            if (collection.TryPop(out var toRemove))
                            {
                                Assert.True(table.TryRemove(toRemove, out _), "Failed to remove");
                            }
                            else
                            {
                                //if (table.Count != 0)
                                //{
                                //    collection.TryDequeue(out toRemove);
                                //}
                                //Assert.AreEqual(0, table.Count);
                            }
                        }
                    }
                });
                threads[k].Start();
            }

            for (int i = 0; i < threads.Length; ++i)
            {
                threads[i].Join();
            }

            Console.WriteLine("Final collection size is " + table.Count);
            while (collection.TryPop(out var toRemove2))
            {
                Assert.True(table.TryRemove(toRemove2, out _));
            }

            Assert.AreEqual(0, table.Count);
        }

        private void Test2(IConcurrentUnorderedCollection<int> collection, int numThreads, int workSize, double seconds)
        {
            int inProgress = 0;

            DateTime now = DateTime.UtcNow;

            Thread[] threads = new Thread[numThreads];
            for (int k = 0; k < threads.Length; ++k)
            {
                Interlocked.Increment(ref inProgress);
                threads[k] = new Thread(() =>
                {
                    int val = 0;
                    while ((DateTime.UtcNow - now).TotalSeconds < seconds)
                    {
                        for (int i = 0; i < workSize; ++i)
                        {
                            Assert.True(collection.Put(val++));
                        }

                        for (int i = 0; i < workSize; ++i)
                        {
                            int tmp;
                            while (!collection.TryPop(out tmp))
                            {
                            }
                        }
                    }
                    Interlocked.Decrement(ref inProgress);
                });
                threads[k].Start();
            }

            while (inProgress != 0)
            {
                Thread.Sleep(100);
            }
        }
    }
}