using System;

namespace Shared
{
    [Serializable]
    public struct DeltaTime
    {
        private readonly int mDeltaMs;

        public static readonly DeltaTime Zero = new DeltaTime(0);
        public static readonly DeltaTime OneSec = new DeltaTime(1000);

        /// <summary>
        /// int max value in milliseconds.
        /// Use this value carefully, it is not supported in operators and can cause overflows
        /// </summary>
        public static readonly DeltaTime Infinity = new DeltaTime(int.MaxValue);

        private DeltaTime(double seconds) : this((int)Math.Round(seconds * 1000.0)) { }
        private DeltaTime(int milliseconds)
        {
            mDeltaMs = milliseconds;
        }

        public static DeltaTime FromSeconds(double time)
        {
            return new DeltaTime(time);
        }

        public static DeltaTime FromMiliseconds(int milisecdonds)
        {
            return new DeltaTime(milisecdonds);
        }

        public static DeltaTime FromSystemTimeSpan(System.TimeSpan timeSpan)
        {
            return new DeltaTime((int)timeSpan.TotalMilliseconds);
        }

        public static DeltaTime Max(DeltaTime dt1, DeltaTime t2)
        {
            return new DeltaTime(System.Math.Max(dt1.mDeltaMs, t2.mDeltaMs));
        }

        public static DeltaTime Min(DeltaTime dt1, DeltaTime t2)
        {
            return new DeltaTime(System.Math.Min(dt1.mDeltaMs, t2.mDeltaMs));
        }


        public int MilliSeconds
        {
            get
            {
                return mDeltaMs;
            }
        }

        public double Seconds
        {
            get
            {
                return mDeltaMs / 1000.0;
            }
        }

        public bool IsZero
        {
            get
            {
                return mDeltaMs == 0;
            }
        }

        public bool IsInfinity
        {
            get
            {
                return mDeltaMs == Infinity.mDeltaMs;
            }
        }

        public static bool operator <(DeltaTime dt1, DeltaTime dt2)
        {
            return dt1.mDeltaMs < dt2.mDeltaMs;
        }

        public static bool operator <=(DeltaTime dt1, DeltaTime dt2)
        {
            return dt1.mDeltaMs <= dt2.mDeltaMs;
        }

        public static bool operator >(DeltaTime dt1, DeltaTime dt2)
        {
            return dt1.mDeltaMs > dt2.mDeltaMs;
        }

        public static bool operator >=(DeltaTime dt1, DeltaTime dt2)
        {
            return dt1.mDeltaMs >= dt2.mDeltaMs;
        }

        public static bool operator ==(DeltaTime dt1, DeltaTime dt2)
        {
            return dt1.mDeltaMs == dt2.mDeltaMs;
        }

        public static bool operator !=(DeltaTime dt1, DeltaTime dt2)
        {
            return dt1.mDeltaMs != dt2.mDeltaMs;
        }

        public override bool Equals(object obj)
        {
            if (obj is DeltaTime)
            {
                return this == (DeltaTime)obj;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return this.mDeltaMs.GetHashCode();
        }

        public override string ToString()
        {
            return mDeltaMs.ToString();
        }

        public static DeltaTime Delta(Time t1, Time t2)
        {
            return new DeltaTime(t1.MilliSeconds - t2.MilliSeconds);
        }

        public static DeltaTime operator -(DeltaTime dTime)
        {
            return new DeltaTime(-dTime.MilliSeconds);
        }

        public static DeltaTime operator -(DeltaTime dTime1, DeltaTime dTime2)
        {
            return new DeltaTime(dTime1.MilliSeconds - dTime2.MilliSeconds);
        }

        public static DeltaTime operator +(DeltaTime dTime1, DeltaTime dTime2)
        {
            return new DeltaTime(dTime1.MilliSeconds + dTime2.MilliSeconds);
        }

        public static DeltaTime operator *(DeltaTime dTime1, int multiply)
        {
            return new DeltaTime(dTime1.MilliSeconds * multiply);
        }

        public static DeltaTime operator *(int multiply, DeltaTime dTime1)
        {
            return new DeltaTime(dTime1.MilliSeconds * multiply);
        }

        public static DeltaTime operator /(DeltaTime dTime1, int div)
        {
            return new DeltaTime(dTime1.MilliSeconds / div);
        }

        public static DeltaTime operator *(DeltaTime dTime, float scale)
        {
            return new DeltaTime((int)(dTime.mDeltaMs * scale + 0.5f));
        }

        public static float operator /(DeltaTime dTime1, DeltaTime dTime2)
        {
            if (dTime2.mDeltaMs != 0)
                return dTime1.mDeltaMs / (float)dTime2.mDeltaMs;
            if (dTime1.mDeltaMs > 0)
                return float.MaxValue;
            if (dTime1.mDeltaMs < 0)
                return float.MinValue;
            return float.NaN;
        }

        public static DeltaTime CropInfinity(DeltaTime dt1, DeltaTime dt2)
        {
            long dt = (long)dt1.MilliSeconds + (long)dt2.MilliSeconds;
            if (dt < (long)Infinity.MilliSeconds)
            {
                return new DeltaTime((int)dt);
            }
            return Infinity;
        }

        public static DeltaTime CropInfinity(DeltaTime dt1, float factor)
        {
            double dt = (double)dt1.MilliSeconds * factor;
            if (dt < (double)Infinity.MilliSeconds)
            {
                return new DeltaTime((int)dt);
            }
            return Infinity;
        }
    }
}