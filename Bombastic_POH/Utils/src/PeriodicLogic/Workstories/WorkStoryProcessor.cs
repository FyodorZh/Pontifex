using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Actuarius.Collections;

namespace Shared.Utils
{
    public class WorkStoryProcessor : IPeriodicLogic
    {
        private class JobsPool
        {
            private readonly PriorityQueue<System.DateTime, IWorkStory> mJobs = new PriorityQueue<System.DateTime, IWorkStory>();
            private readonly ThreadSafeDateTime mHeadTime = new ThreadSafeDateTime();
            private volatile int mSize;

            public void Append(IWorkStory job)
            {
                lock (mJobs)
                {
                    mJobs.Enqueue(job.NextTickTime, job);
                    mHeadTime.Time = mJobs.TopKey();
                    mSize = mJobs.Count;
                }
            }

            public IWorkStory Extract(DateTime tillTime)
            {
                if (mSize > 0 && mHeadTime.Time <= tillTime)
                {
                    lock (mJobs)
                    {
                        if (mJobs.Count > 0 && mJobs.TopKey() <= tillTime)
                        {
                            var res = mJobs.Dequeue();
                            mHeadTime.Time = mJobs.TopKey();
                            mSize = mJobs.Count;
                            return res;
                        }
                    }
                }

                return null;
            }
        }

        private class Worker : IPeriodicLogic
        {
            private readonly WorkStoryProcessor mOwner;

            private volatile ILogicDriverCtl mDriver;

            private volatile Thread mCurThread;
            private readonly ThreadSafeDateTime mCurJobEndTime = new ThreadSafeDateTime();

            private volatile IWorkStory mCurJob;

            private readonly Stopwatch mTimer = new Stopwatch();

            public Thread CurrentThread
            {
                get { return mCurThread; }
            }

            public IWorkStory CurrentJob
            {
                get { return mCurJob; }
            }

            public Worker(WorkStoryProcessor owner)
            {
                mOwner = owner;
            }

            public bool LogicStarted(ILogicDriverCtl driver)
            {
                mDriver = driver;
                return true;
            }

            public void LogicStopped()
            {
                mDriver = null;
            }

            public void LogicTick()
            {
                mTimer.Reset();
                mTimer.Start();

                while (true)
                {
                    var now = HighResDateTime.UtcNow;

                    IWorkStory job = mOwner.Jobs.Extract(now);
                    if (job == null)
                    {
                        break;
                    }

                    WorkstoryState workState;
                    try
                    {
                        mCurJobEndTime.Time = now.AddMilliseconds(job.MaxTickTime.MilliSeconds);
                        mCurThread = Thread.CurrentThread;
                        mCurJob = job;
                        job.Tick(now);
                        workState = job.IsFinished ? WorkstoryState.Done : WorkstoryState.InProgress;
                    }
                    catch (ThreadAbortException)
                    {
                        string str;
                        try
                        {
                            str = job.ToString();
                        }
                        catch
                        {
                            str = "~?!";
                        }

                        Log.e("Work '" + str + "' hung");
                        workState = WorkstoryState.Hung;
                        mOwner.OnAborted(this);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                        workState = WorkstoryState.Crashed;
                    }
                    finally
                    {
                        mCurThread = null;
                        mCurJob = null;
                    }

                    if (workState != WorkstoryState.InProgress)
                    {
                        mOwner.OnFinished(job, workState);
                        if (workState == WorkstoryState.Hung)
                        {
                            mDriver.Stop();
                            break;
                        }
                    }
                    else
                    {
                        mOwner.Jobs.Append(job);
                    }
                }

                mTimer.Stop();
                mOwner.LogWork(mTimer.Elapsed);
            }

            public void Stop()
            {
                var driver = mDriver;
                if (driver != null)
                {
                    driver.Stop();
                }
            }

            public bool CheckForHung()
            {
                var thread = mCurThread;
                if (thread != null && mCurJobEndTime.Time <= HighResDateTime.UtcNow)
                {
                    return true;
                }

                return false;
            }
        }

        private readonly IPeriodicLogicRunner mDriverSource;
        private readonly Worker[] mWorkers;
        private readonly JobsPool mJobs;

        private readonly DeltaTime mTickPeriod;

        private readonly Action<IWorkStory, WorkstoryState> mOnFinish;
        private readonly Action<IWorkStory, Thread> mOnHung;

        private readonly IConcurrentQueue<KeyValuePair<IWorkStory, WorkstoryState>> mFinished = new TinyConcurrentQueue<KeyValuePair<IWorkStory, WorkstoryState>>();

        private volatile ILogicDriverCtl mDriver;

        private int mActiveJobsCount;

        private readonly WorkTimeAggregator mWorkAggregator;
        private readonly System.TimeSpan mStatisticsFlushPeriod;
        private DateTime mStatisticsFlushTime;


        public WorkStoryProcessor(IPeriodicLogicRunner driverSource,
            int driverPoolSize,
            DeltaTime tickPeriod,
            Action<IWorkStory, WorkstoryState> onFinish,
            Action<IWorkStory, Thread> onHung,
            IPerformanceMonitor monitor = null)
        {
            mDriverSource = driverSource;
            mWorkers = new Worker[driverPoolSize];
            mJobs = new JobsPool();
            mOnFinish = onFinish;
            mOnHung = onHung;

            mWorkAggregator = monitor != null ? new WorkTimeAggregator(monitor, driverPoolSize) : null;
            mStatisticsFlushPeriod = monitor != null ? monitor.UpdatePeriod : System.TimeSpan.Zero;

            mTickPeriod = tickPeriod;

            for (int i = 0; i < mWorkers.Length; ++i)
            {
                if (mWorkers[i] == null)
                {
                    var worker = new Worker(this);
                    if (mDriverSource.Run(worker, mTickPeriod) != null)
                    {
                        mWorkers[i] = worker;
                    }
                }
            }
        }

        public int JobsCount
        {
            get { return mActiveJobsCount; }
        }

        public bool IsStarted
        {
            get { return mDriver != null; }
        }

        public bool AddJob(IWorkStory job)
        {
            if (IsStarted)
            {
                if (job != null && !job.IsFinished)
                {
                    Interlocked.Increment(ref mActiveJobsCount);
                    mJobs.Append(job);
                    return true;
                }
            }

            return false;
        }

        public void Stop()
        {
            var driver = mDriver;
            if (driver != null)
            {
                driver.Stop();
            }
        }

        private JobsPool Jobs
        {
            get { return mJobs; }
        }

        private void OnFinished(IWorkStory job, WorkstoryState result)
        {
            mFinished.Put(new KeyValuePair<IWorkStory, WorkstoryState>(job, result));
        }

        private void OnAborted(Worker worker)
        {
            for (int i = 0; i < mWorkers.Length; ++i)
            {
                if (mWorkers[i] == worker)
                {
                    worker = new Worker(this);
                    if (mDriverSource.Run(worker, mTickPeriod) == null)
                    {
                        worker = null;
                    }

                    mWorkers[i] = worker;
                }
            }
        }

        private void LogWork(System.TimeSpan time)
        {
            if (mWorkAggregator != null)
            {
                mWorkAggregator.Register(time);
            }
        }

        bool IPeriodicLogic.LogicStarted(ILogicDriverCtl driver)
        {
            mDriver = driver;
            mStatisticsFlushTime = DateTime.UtcNow.Add(mStatisticsFlushPeriod);
            return true;
        }

        void IPeriodicLogic.LogicTick()
        {
            for (int i = 0; i < mWorkers.Length; ++i)
            {
                var worker = mWorkers[i];
                if (worker != null)
                {
                    if (worker.CheckForHung())
                    {
                        Log.e("WORKSTORY HUNG DETECTED!!!");
                        try
                        {
                            mOnHung(worker.CurrentJob, worker.CurrentThread);
                        }
                        catch (Exception e)
                        {
                            Log.wtf(e);
                        }

                        worker.Stop();
                        OnAborted(worker);
                        OnFinished(worker.CurrentJob, WorkstoryState.Hung);
                    }
                }
            }

            KeyValuePair<IWorkStory, WorkstoryState> kv;
            while (mFinished.TryPop(out kv))
            {
                Interlocked.Decrement(ref mActiveJobsCount);
                if (mOnFinish != null)
                {
                    try
                    {
                        mOnFinish(kv.Key, kv.Value);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                }
            }

            var now = HighResDateTime.UtcNow;
            if (mStatisticsFlushTime <= now)
            {
                mStatisticsFlushTime = now.Add(mStatisticsFlushPeriod);
                if (mWorkAggregator != null)
                {
                    mWorkAggregator.Flush();
                }
            }
        }

        void IPeriodicLogic.LogicStopped()
        {
            mDriver = null;
            for (int i = 0; i < mWorkers.Length; ++i)
            {
                if (mWorkers[i] != null)
                {
                    mWorkers[i].Stop();
                    mWorkers[i] = null;
                }
            }
        }
    }
}