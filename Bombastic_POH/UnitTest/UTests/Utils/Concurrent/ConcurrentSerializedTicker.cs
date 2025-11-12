using System.Threading;
using NUnit.Framework;
using Shared.Concurrent;

namespace TransportAnalyzer.UTests.Concurrent
{
    [TestFixture]
    public class ConcurrentSerializedTickerTest
    {
        [Test]
        public void Test()
        {
            int _id = 0;
            ConcurrentSerializedTicker mTicker = new ConcurrentSerializedTicker(() => { _id += 1; });

            Thread[] threads = new Thread[20];
            for (int i = 0; i < threads.Length; ++i)
            {
                threads[i] = new Thread(() =>
                {
                    for (int j = 0; j < 1000000; ++j)
                    {
                        mTicker.Tick();
                    }
                });
                threads[i].Start();
            }

            for (int i = 0; i < threads.Length; ++i)
            {
                threads[i].Join(1000000);
            }

            Assert.AreEqual(threads.Length * 1000000, _id);
        }
    }
}