using System.Threading;
using Actuarius.Collections;
using NUnit.Framework;
using Shared;
using Shared.Concurrent;

namespace TransportAnalyzer.UTests.Concurrent
{
    [TestFixture]
    public class ActionQueueTest
    {
        private int mId;

        private struct TAction : ActionQueue<TAction>.IAction
        {
            private readonly ActionQueueTest mOwner;

            public TAction(ActionQueueTest owner)
            {
                mOwner = owner;
            }

            public void Invoke()
            {
                mOwner.mId += 1;
            }

            public void Fail()
            {
                Assert.Fail();
            }
        }

        [Test]
        public void Test()
        {
            mId = 0;

            ActionQueue<TAction> mQueue = new ActionQueue<TAction>(new TinyConcurrentQueue<TAction>());

            Thread[] threads = new Thread[20];
            for (int i = 0; i < threads.Length; ++i)
            {
                threads[i] = new Thread(() =>
                {
                    for (int j = 0; j < 1000000; ++j)
                    {
                        mQueue.Put(new TAction(this));
                    }
                });
                threads[i].Start();
            }

            for (int i = 0; i < threads.Length; ++i)
            {
                threads[i].Join(1000000);
            }

            Assert.AreEqual(threads.Length * 1000000, mId);
        }
    }
}