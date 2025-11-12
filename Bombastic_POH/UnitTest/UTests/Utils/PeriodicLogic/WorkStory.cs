using System;
using NUnit.Framework;
using Shared;
using Shared.Utils;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    class WorkStoryTest
    {
        private class CountJob : IWorkStory
        {
            public int Result10;

            bool IWorkStory.IsFinished
            {
                get { return Result10 == 10; }
            }

            DateTime IWorkStory.NextTickTime
            {
                get { return DateTime.UtcNow.AddMilliseconds(10); }
            }

            DeltaTime IWorkStory.MaxTickTime
            {
                get { return DeltaTime.FromSeconds(1); }
            }

            public virtual void Tick(DateTime now)
            {
                Result10 += 1;
            }
        }

        private class CrashJob : CountJob
        {
            public override void Tick(DateTime now)
            {
                base.Tick(now);
                if (Result10 == 5)
                {
                    throw new Exception();
                }
            }
        }

        private class HungJob : CountJob
        {
            public override void Tick(DateTime now)
            {
                base.Tick(now);
                if (Result10 == 5)
                {
                    while (true) ;
                }
            }
        }

        private class PeriodicLogic : PeriodicLogicWorkStoryDriver
        {
            public Job JobObj;

            public class Job : IPeriodicLogic
            {
                public int Result10;

                private ILogicDriverCtl mDriver;

                public void LogicTick()
                {
                    Result10 += 1;
                    if (Result10 == 10)
                    {
                        mDriver.Stop();
                    }
                }

                public void LogicStopped()
                {
                }

                public bool LogicStarted(ILogicDriverCtl driver)
                {
                    mDriver = driver;
                    return true;
                }
            }

            public PeriodicLogic(WorkStoryProcessor processor)
                : base(processor, DeltaTime.FromMiliseconds(10), DeltaTime.FromSeconds(1))
            {
                JobObj = new Job();
            }

            public void Start()
            {
                Assert.IsTrue(this.Start(JobObj.Test(Assert.Fail), Log.ConsoleLogger));
            }
        }

        [Test]
        public void MainTest()
        {
            System.Action<IWorkStory, WorkstoryState> onJobDone = (story, result) =>
            {
                if (story is CrashJob)
                {
                    Assert.AreEqual(WorkstoryState.Crashed, result);
                    Assert.AreNotEqual(10, ((CrashJob)story).Result10);
                }
                else if (story is HungJob)
                {
                    Assert.AreEqual(WorkstoryState.Hung, result);
                    Assert.AreNotEqual(10, ((HungJob)story).Result10);
                }
                else if (story is CountJob)
                {
                    Assert.AreEqual(WorkstoryState.Done, result);
                    Assert.AreEqual(10, ((CountJob)story).Result10);
                }
                else if (story is PeriodicLogic)
                {
                    Assert.AreEqual(WorkstoryState.Done, result);
                    Assert.AreEqual(10, ((PeriodicLogic)story).JobObj.Result10);
                }
            };
            int workerPoolSize = 3;
            var runner = new PeriodicLogicThreadedRunner(Log.StaticLogger);
            WorkStoryProcessor processor = new WorkStoryProcessor(runner, workerPoolSize, DeltaTime.FromMiliseconds(1),  onJobDone, (job, thread)=> { thread.Abort(); });
            runner.Run(processor, DeltaTime.FromMiliseconds(1));

            Assert.IsTrue(processor.AddJob(new CountJob()));
            Assert.IsTrue(processor.AddJob(new CrashJob()));
            Assert.IsTrue(processor.AddJob(new HungJob()));
            new PeriodicLogic(processor).Start();

            var now = DateTime.UtcNow;
            while (processor.JobsCount != 0)
            {
                System.Threading.Thread.Sleep(100);
                if ((DateTime.UtcNow - now).TotalSeconds > 5)
                {
                    Assert.Fail("Fail to finish");
                    break;
                }
            }
        }
    }
}
