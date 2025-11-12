using Serializer.BinarySerializer;

namespace Shared
{
    public struct Time : System.IComparable<Time>, IDataStruct
    {
        private int mTimeMs;

        public static readonly Time Zero = new Time(0);
        public static readonly Time SecondInPast = new Time(-1000);

        /// <summary>
        /// int max value in milliseconds.
        /// Use this value carefully, it is not supported in operators and can cause overflows
        /// </summary>
        public static readonly Time Infinity = new Time(int.MaxValue);

        private Time(double seconds) : this((int)System.Math.Round(seconds * 1000.0)) { }
        private Time(int milliseconds)
        {
            mTimeMs = milliseconds;
        }

        public static Time FromMiliseconds(int millisecdonds)
        {
            return new Time(millisecdonds);
        }

        public static Time FromSeconds(double time)
        {
            return new Time(time);
        }

        public static Time Max(Time t1, Time t2)
        {
            return new Time(System.Math.Max(t1.mTimeMs, t2.mTimeMs));
        }

        public static Time Min(Time t1, Time t2)
        {
            return new Time(System.Math.Min(t1.mTimeMs, t2.mTimeMs));
        }

        public int MilliSeconds
        {
            get
            {
                return mTimeMs;
            }
        }

        public double Seconds
        {
            get
            {
                return (double)(mTimeMs) / 1000.0;
            }
        }

        public DeltaTime FromZero
        {
            get
            {
                return DeltaTime.FromMiliseconds(mTimeMs);
            }
        }

        public bool IsZero
        {
            get
            {
                return mTimeMs == 0;
            }
        }

        public bool IsInfinity
        {
            get
            {
                return mTimeMs == Infinity.mTimeMs;
            }
        }

        public int CompareTo(Time other)
        {
            return mTimeMs.CompareTo(other.mTimeMs);
        }

        public static bool operator <(Time t1, Time t2)
        {
            return t1.mTimeMs < t2.mTimeMs;
        }

        public static bool operator >(Time t1, Time t2)
        {
            return t1.mTimeMs > t2.mTimeMs;
        }

        public static bool operator <=(Time t1, Time t2)
        {
            return t1.mTimeMs <= t2.mTimeMs;
        }

        public static bool operator >=(Time t1, Time t2)
        {
            return t1.mTimeMs >= t2.mTimeMs;
        }

        public static bool operator ==(Time t1, Time t2)
        {
            return t1.mTimeMs == t2.mTimeMs;
        }

        public static bool operator !=(Time t1, Time t2)
        {
            return t1.mTimeMs != t2.mTimeMs;
        }

        public override bool Equals(object obj)
        {
            if (obj is Time)
            {
                return this == (Time)obj;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return this.mTimeMs.GetHashCode();
        }

        public static Time operator +(Time time, DeltaTime dTime)
        {
            return new Time(time.MilliSeconds + dTime.MilliSeconds);
        }

        public static Time operator +(DeltaTime dTime, Time time)
        {
            return time + dTime;
        }

        public static DeltaTime operator -(Time time1, Time time2)
        {
            return DeltaTime.FromMiliseconds(time1.MilliSeconds - time2.MilliSeconds);
        }

        public static Time operator -(Time time, DeltaTime dTime)
        {
            return Time.FromMiliseconds(time.MilliSeconds - dTime.MilliSeconds);
        }

        public override string ToString()
        {
            return "[T:" + mTimeMs + "]";
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref mTimeMs);
            return true;
        }

        public static Time CropInfinity(Time time, DeltaTime dTime)
        {
            long t = (long)time.MilliSeconds + (long)dTime.MilliSeconds;
            if (t < (long)Infinity.MilliSeconds)
            {
                return new Time((int)t);
            }
            return Infinity;
        }

        public static Time CropInfinity(DeltaTime dTime, Time time)
        {
            return CropInfinity(time, dTime);
        }
    }
}