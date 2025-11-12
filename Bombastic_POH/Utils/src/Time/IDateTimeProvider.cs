using System;

namespace Shared
{
    public interface IDateTimeProvider
    {
        DateTime Now { get; }
    }

    public interface IUtcDateTimeProvider : IDateTimeProvider
    {
    }

    public class UtcNowDateTimeProvider : IUtcDateTimeProvider
    {
        public static readonly IDateTimeProvider Instance = new UtcNowDateTimeProvider();

        private UtcNowDateTimeProvider()
        {
        }

        public DateTime Now
        {
            get { return DateTime.UtcNow; }
        }
    }

    public class NowDateTimeProvider : IDateTimeProvider
    {
        public static readonly IDateTimeProvider Instance = new NowDateTimeProvider();

        private NowDateTimeProvider()
        {
        }

        public DateTime Now
        {
            get { return DateTime.Now; }
        }
    }
}