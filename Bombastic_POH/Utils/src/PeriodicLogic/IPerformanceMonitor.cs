using System;

namespace Shared.Utils
{
    public interface IPerformanceMonitor
    {
        System.TimeSpan UpdatePeriod { get; }

        /// <summary>
        /// Информирует о текущей нагрузке в диапазоне [0..1]
        /// Возможны кратковременные аномалии, когда величина нагрузки становится больше 1
        /// </summary>
        void Set(double performance);
    }

    public class WorkTimeAggregator
    {
        private readonly IPerformanceMonitor mMonitor;
        private readonly long mWorkers;

        private long mTime;

        private DateTime mFlushTime;

        public WorkTimeAggregator(IPerformanceMonitor monitor, int workers)
        {
            mMonitor = monitor;
            mWorkers = Math.Max(workers, 1);
            mFlushTime = HighResDateTime.UtcNow;
        }

        public void Register(System.TimeSpan currentLoad)
        {
            System.Threading.Interlocked.Add(ref mTime, (long)(currentLoad.TotalMilliseconds + 0.5));
        }

        public void Flush()
        {
            long workTime = System.Threading.Interlocked.Exchange(ref mTime, 0);

            var now = HighResDateTime.UtcNow;

            var dt = (now - mFlushTime).TotalMilliseconds;
            mFlushTime = now;

            mMonitor.Set(workTime / (mWorkers * dt));
        }
    }
}