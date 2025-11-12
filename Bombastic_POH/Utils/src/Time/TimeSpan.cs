using Serializer.BinarySerializer;

namespace Shared
{
    /// <summary>
    /// Временной интервал. Допускает нулевую длину.
    /// Start всегда не больше чем End
    /// </summary>
    public struct TimeSpan : IDataStruct
    {
        private int mEndTime;
        private int mLength;

        public static readonly TimeSpan Empty = new TimeSpan(0, -1);
        public static readonly TimeSpan ZeroFromZero = new TimeSpan(0, 0);

        private TimeSpan(int startTime, int endTime)
        {
            mLength = endTime - startTime;
            mEndTime = endTime;
        }

        public TimeSpan(Time startTime, DeltaTime length)
        {
            mLength = length.MilliSeconds;

            DBG.Diagnostics.Assert(mLength >= 0);
            if (mLength >= 0)
            {
                mEndTime = startTime.MilliSeconds + mLength;
            }
            else
            {
                mLength = -mLength;
                mEndTime = startTime.MilliSeconds;
            }
        }

        public TimeSpan(DeltaTime length, Time endTime)
        {
            mLength = length.MilliSeconds;

            DBG.Diagnostics.Assert(mLength >= 0);
            if (mLength >= 0)
            {
                mEndTime = endTime.MilliSeconds;
            }
            else
            {
                mLength = -mLength;
                mEndTime = endTime.MilliSeconds + mLength;
            }
        }

        public TimeSpan(Time startTime, Time endTime)
        {
            mLength = endTime.MilliSeconds - startTime.MilliSeconds;

            DBG.Diagnostics.Assert(mLength >= 0);
            if (mLength >= 0)
            {
                mEndTime = endTime.MilliSeconds;
            }
            else
            {
                mLength = -mLength;
                mEndTime = startTime.MilliSeconds;
            }
        }

        /// <summary>
        /// Объеденяет два интервала в один.
        /// </summary>
        public static TimeSpan Merge(TimeSpan s1, TimeSpan s2)
        {
            if (s1.IsEmpty)
            {
                return s2;
            }
            if (s2.IsEmpty)
            {
                return s1;
            }

            DBG.Diagnostics.Assert(s1.End == s2.Start, "TimeSpan merge : {0} + {1}", s1, s2);
            int start = System.Math.Min(s1.mEndTime - s1.mLength, s2.mEndTime - s2.mLength);
            int end = System.Math.Max(s1.mEndTime, s2.mEndTime);
            return new TimeSpan(start, end);
        }

        public TimeSpan Prolong(Time tillTime)
        {
            if (IsEmpty)
            {
                return new TimeSpan(tillTime, DeltaTime.Zero);
            }

            DBG.Diagnostics.Assert(End <= tillTime);
            if (End <= tillTime)
            {
                return new TimeSpan(mEndTime - mLength, tillTime.MilliSeconds);
            }
            return this;
        }

        public TimeSpan Shrink(Time fromTime)
        {
            if (IsEmpty)
            {
                return this;
            }

            DBG.Diagnostics.Assert(Contains(fromTime));
            if (Contains(fromTime))
            {
                return new TimeSpan(fromTime.MilliSeconds, mEndTime);
            }
            return new TimeSpan(fromTime.MilliSeconds, fromTime.MilliSeconds);
        }

        public bool IsEmpty
        {
            get
            {
                return this == Empty;
            }
        }

        public Time Start
        {
            get
            {
                if (IsEmpty)
                {
                    DBG.Diagnostics.Assert(false);
                    return Time.Zero;
                }
                return Time.FromMiliseconds(mEndTime - mLength);
            }
        }

        public Time End
        {
            get
            {
                if (IsEmpty)
                {
                    DBG.Diagnostics.Assert(false);
                    return Time.Zero;
                }
                return Time.FromMiliseconds(mEndTime);
            }
        }

        public DeltaTime Length
        {
            get
            {
                if (IsEmpty)
                {
                    DBG.Diagnostics.Assert(false);
                    return DeltaTime.Zero;
                }
                return DeltaTime.FromMiliseconds(mLength);
            }
        }

        public bool Contains(Time time)
        {
            if (IsEmpty)
            {
                return false;
            }            
            int t = time.MilliSeconds;
            return (t >= mEndTime - mLength && t <= mEndTime);
        }

        public bool Contains(TimeSpan span)
        {
            if (IsEmpty || span.IsEmpty)
            {
                return false;
            }
            return (span.mEndTime - span.mLength >= mEndTime - mLength) && (span.mEndTime <= mEndTime);
        }

        public Time Clamp(Time time)
        {
            time = Time.Min(time, End);
            time = Time.Max(time, Start);
            return time;
        }

        public Time Lerp(float t01)
        {
            return Start + Length * t01;
        }

        public static TimeSpan operator +(TimeSpan span, DeltaTime dTime)
        {
            if (span.IsEmpty)
            {
                return span;
            }
            int dt = dTime.MilliSeconds;
            return new TimeSpan(span.mEndTime - span.mLength + dt, span.mEndTime + dt);
        }

        public static TimeSpan operator -(TimeSpan span, DeltaTime dTime)
        {
            if (span.IsEmpty)
            {
                return span;
            }
            int dt = -dTime.MilliSeconds;
            return new TimeSpan(span.mEndTime - span.mLength + dt, span.mEndTime + dt);
        }

        public static bool operator ==(TimeSpan t1, TimeSpan t2)
        {
            return t1.mLength == t2.mLength && t1.mEndTime == t2.mEndTime;
        }

        public static bool operator !=(TimeSpan t1, TimeSpan t2)
        {
            return t1.mLength != t2.mLength || t1.mEndTime != t2.mEndTime;
        }

        public override bool Equals(object obj)
        {
            if (obj is TimeSpan)
            {
                return this == (TimeSpan)obj;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return mEndTime ^ mLength;
        }

        public override string ToString()
        {
            return "[" + (mEndTime - mLength) + "; " + mEndTime + "]";
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref mEndTime);
            dst.Add(ref mLength);
            return true;
        }
    }
}