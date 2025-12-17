using System;
using Actuarius.PeriodicLogic;
using Pontifex.Abstractions.Controls;

namespace Transport.Utils
{
    public class DeliveryController : IDeliveryController, IDeliveryControllerSink
    {
        private readonly IDateTimeProvider mTimeProvider;

        private int mDelivered;
        private int mDeliveredAccumulator;

        private int mDeliveryAttempts;
        private int mDeliveryAttemptsAccumulator;

        private DateTime mStatisticsDropTime;

        private readonly object mLocker = new object();

        public string Name { get; private set; }

        public DeliveryController(string name, IDateTimeProvider timeProvider)
        {
            Name = name;
            mTimeProvider = timeProvider;
        }

        int IDeliveryController.DeliveredPS => mDelivered;

        int IDeliveryController.AttemptsPS => mDeliveryAttempts;

        void IDeliveryControllerSink.AttemptToDeliver(bool first)
        {
            if (first)
            {
                System.Threading.Interlocked.Increment(ref mDeliveredAccumulator);
                System.Threading.Interlocked.Increment(ref mDeliveryAttemptsAccumulator);
            }
            else
            {
                System.Threading.Interlocked.Increment(ref mDeliveryAttemptsAccumulator);
            }
        }

        public void Refresh()
        {
            var now = mTimeProvider.Now;
            if (now > mStatisticsDropTime)
            {
                lock (mLocker)
                {
                    if (now > mStatisticsDropTime)
                    {
                        mStatisticsDropTime = now.AddSeconds(1);

                        mDelivered = mDeliveredAccumulator;
                        mDeliveryAttempts = mDeliveryAttemptsAccumulator;

                        mDeliveredAccumulator = 0;
                        mDeliveryAttemptsAccumulator = 0;
                    }
                }
            }
        }
    }
}