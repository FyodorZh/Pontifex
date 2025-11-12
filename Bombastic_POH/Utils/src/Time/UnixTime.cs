using System;

namespace Shared
{
    public static class UnixTime
    {
        public const long MILLIS_PER_SEC = 1000;
        public static readonly DateTime BEGIN_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static int UnixTimeNow()
        {
            return DateTimeToUnixTime(DateTime.UtcNow);
        }

        public static long UnixTimeNowMs()
        {
            return DateTimeToUnixTimeMs(DateTime.UtcNow);
        }

        public static int MillisToSec(long millis)
        {
            return (int) (millis/MILLIS_PER_SEC);
        }

        public static long SecToMillis(int seconds)
        {
            return seconds*MILLIS_PER_SEC;
        }

        public static DateTime UnixTimeNowServer(System.TimeSpan serverTimeDelta)
        {
            return DateTime.UtcNow + serverTimeDelta;
        }

        public static DateTime UnixTimeToDateTime(long seconds)
        {
            if (seconds <= 0)
            {
                return DateTime.MinValue;
            }

            return BEGIN_EPOCH.AddSeconds((double)seconds);
        }

        public static DateTime UnixTimeToDateTimeMs(long milliseconds)
        {
            if (milliseconds <= 0)
            {
                return DateTime.MinValue;
            }

            return BEGIN_EPOCH.AddMilliseconds((double)milliseconds);
        }

        /// <summary>
        /// Returns the number of seconds since epoch UTC (1 january 1970 0:00:00)
        /// </summary>
        public static int DateTimeToUnixTime(DateTime dt)
        {
            if (dt == System.DateTime.MinValue)
            {
                return 0;
            }

            var timeSpan = (dt.ToUniversalTime() - BEGIN_EPOCH);
            return (int)timeSpan.TotalSeconds;
        }

        /// <summary>
        /// Returns the number of milliseconds since epoch UTC (1 january 1970 0:00:00)
        /// </summary>
        public static long DateTimeToUnixTimeMs(DateTime dt)
        {
            if (dt == System.DateTime.MinValue)
            {
                return 0;
            }

            var timeSpan = (dt.ToUniversalTime() - BEGIN_EPOCH);
            return (long)timeSpan.TotalMilliseconds;
        }

        public static string FloatToTime(float value, string format)
        {
            int minute = (int)value / 60;
            int seconds = (int)value % 60;
            return string.Format(format, minute, seconds.ToString("00"));
        }

        public static string GetHoursMinutesTimerByServerTimestamp(long serverTimeStamp, System.TimeSpan serverToLocalDelta)
        {
            DateTime actualServerTime = DateTime.UtcNow + serverToLocalDelta;
            long unixTime = DateTimeToUnixTime(actualServerTime);

            long delta = serverTimeStamp - unixTime;
            if (delta < 0)
            {
                return "0:00";
            }

            int hours = (int)delta / 3600;
            int minutes = (int)((delta % 3600) / 60);
            return string.Format("{0:00}:{1:00}", hours, minutes); ;
        }

        public static int FutureUnixTime(int offset)
        {
            return UnixTimeNow() + offset;
        }

		public static long FutureUnixTimeMs(long offsetMs)
		{
            return UnixTimeNowMs() + offsetMs;
		}

        public static bool IsExpiredTime(int timeSec)
        {
            return timeSec <= UnixTimeNow();
        }

        public static bool IsExpiredTimeMs(long timeMs)
        {
            return timeMs <= UnixTimeNowMs();
        }
    }

    public static class DateTimeExtensions
    {
        public static int ToUnixTime(this DateTime dt)
        {
            return UnixTime.DateTimeToUnixTime(dt);
        }
        
        public static int? ToUnixTime(this DateTime? dt)
        {
            if (dt.HasValue)
            {
                return UnixTime.DateTimeToUnixTime(dt.Value);
            }

            return null;
        }
    }
}
