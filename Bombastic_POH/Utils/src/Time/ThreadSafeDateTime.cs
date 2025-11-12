using System;

namespace Shared
{
    public class ThreadSafeDateTime : IDateTimeProvider
    {
        private long mTime;

        public ThreadSafeDateTime()
            : this(new DateTime())
        { }

        public ThreadSafeDateTime(DateTime time)
        {
            Time = time;
        }

        public DateTime Time
        {
            get
            {
                long time = System.Threading.Interlocked.Read(ref mTime);
                return DateTime.FromBinary(time);
            }
            set
            {
                long time = value.ToBinary();
                System.Threading.Interlocked.Exchange(ref mTime, time);
            }
        }

        public DateTime Now
        {
            get { return Time; }
        }
    }
}
