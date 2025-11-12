//using System;
//using LogConsumers;
//using NUnit.Framework;
//using Shared;
//using Shared.Pooling;
//using Shared.Utils;
//
//namespace TransportAnalyzer.UTests
//{
//    [TestFixture]
//    public class ThreadStaticPool
//    {
//        private class Collectable : MultiRefCollectable<Collectable>
//        {
//            private volatile int mState = 1;
//
//            public int Data { get; set; }
//
//            protected override void OnCollected()
//            {
//                Assert.True(System.Threading.Interlocked.Exchange(ref mState, 0) == 1);
//            }
//
//            protected override void OnRestored()
//            {
//                mState = 1;
//            }
//
//            protected override void OnRefCountError(ErrorType error)
//            {
//                Assert.Fail(error.ToString());
//                base.OnRefCountError(error);
//            }
//
//            ~Collectable()
//            {
//                Assert.AreEqual(mState, 0);
//            }
//        }
//
//        private class Job : IWorkStory
//        {
//            private const int N = 1000;
//            private readonly ThreadStaticPoolContext mContext = new ThreadStaticPoolContext();
//
//            private int mTickId;
//
//            private byte[] mBytes;
//
//            public bool IsFinished => mTickId == N;
//
//            public DateTime NextTickTime { get; } = DateTime.UtcNow;
//
//            public DeltaTime MaxTickTime => DeltaTime.Infinity;
//
//            public void Tick()
//            {
////                ++mTickId;
////
////                Assert.IsTrue(mContext.Push() == null);
////
////                if (mTickId == 1)
////                {
////                    mBytes = ThreadStaticPools.Pow2ByteArrays.Acquire(1);
////                    mBytes[0] = 1;
////                }
////
////                var collectable = ThreadStaticPools.ConstructCollectable<Collectable>();
////
////                Assert.AreEqual(mTickId % 256, mBytes[0]);
////                mBytes[0] = (byte)((mTickId + 1) % 256);
////
////                collectable.Release();
////
////                if (mTickId == N)
////                {
////                    ThreadStaticPools.Pow2ByteArrays.Release(mBytes);
////                }
////
////                Assert.IsTrue(ThreadStaticPoolContext.PopCurrent() == mContext);
//            }
//        }
//
//        [Test]
//        public void TestCollectable()
//        {
//            Log.AddConsumer(new StudioDebugConsumer(), true);
//
//            var runner = new PeriodicLogicThreadedRunner(Log.StaticLogger);
//            WorkStoryProcessor processor = new WorkStoryProcessor(runner, 4, DeltaTime.FromMiliseconds(1),
//                (story, state) => Assert.AreEqual(WorkstoryState.Done, state),
//                (story, thread) => Assert.Fail());
//
//            runner.Run(processor, DeltaTime.Zero);
//
//            for (int i = 0; i < 100; ++i)
//            {
//                processor.AddJob(new Job());
//            }
//
//            while (processor.JobsCount > 0)
//            {
//                System.Threading.Thread.Sleep(100);
//            }
//
//            System.Threading.Thread.Sleep(100);
//            System.GC.Collect(2, GCCollectionMode.Forced);
//        }
//    }
//}