using Serializer.BinarySerializer;
using Shared;

namespace Serializer.Extensions
{
    public static class TimeExtensions
    {
        public static void AddTime(this IBinarySerializer serializer, ref Time v)
        {
            if (serializer.isReader)
            {
                v = ReadTime(serializer);
            }
            else
            {
                WriteTime(serializer, ref v);
            }
        }

        public static void AddTime(this IBinarySerializer serializer, ref Time[] times)
        {
            if (serializer.isReader)
            {
                var reader = (IDataReader)serializer;
                if (!reader.GetArray(ref times))
                {
                    return;
                }

                for (int i = 0; i < times.Length; ++i)
                {
                    times[i] = ReadTime(serializer);
                }
            }
            else
            {
                var writer = (IDataWriter)serializer;
                if (!writer.PrepareWriteArray(times))
                {
                    return;
                }

                for (int i = 0; i < times.Length; ++i)
                {
                    var tmp = times[i];
                    WriteTime(serializer, ref tmp);
                }
            }
        }

        public static void AddTimeSpan(this IBinarySerializer serializer, ref TimeSpan v)
        {
            if (serializer.isReader)
            {
                int tempLength = 0;
                serializer.Add(ref tempLength);

                int tempEnd = 0;
                serializer.Add(ref tempEnd);

                v = new TimeSpan(DeltaTime.FromMiliseconds(tempLength), Time.FromMiliseconds(tempEnd));
            }
            else
            {
                var tempLength = v.Length.MilliSeconds;
                serializer.Add(ref tempLength);

                var tempEnd = v.End.MilliSeconds;
                serializer.Add(ref tempEnd);
            }
        }

        public static void AddDeltaTime(this IBinarySerializer serializer, ref DeltaTime v)
        {
            if (serializer.isReader)
            {
                v = ReadDeltaTime(serializer);
            }
            else
            {
                WriteDeltaTime(serializer, ref v);
            }
        }

        public static void AddDeltaTime(this IBinarySerializer serializer, ref DeltaTime[] deltaTimes)
        {
            if (serializer.isReader)
            {
                var reader = (IDataReader)serializer;
                if (!reader.GetArray(ref deltaTimes))
                {
                    return;
                }

                for (int i = 0; i < deltaTimes.Length; ++i)
                {
                    deltaTimes[i] = ReadDeltaTime(serializer);
                }
            }
            else
            {
                var writer = (IDataWriter)serializer;
                if (!writer.PrepareWriteArray(deltaTimes))
                {
                    return;
                }

                for (int i = 0; i < deltaTimes.Length; ++i)
                {
                    var tmp = deltaTimes[i];
                    WriteDeltaTime(serializer, ref tmp);
                }
            }
        }

        private static Time ReadTime(IBinarySerializer serializer)
        {
            int tempInt = 0;
            serializer.Add(ref tempInt);
            return Time.FromMiliseconds(tempInt);
        }

        private static void WriteTime(IBinarySerializer serializer, ref Time v)
        {
            int tempMs = v.MilliSeconds;
            serializer.Add(ref tempMs);
        }

        private static void WriteDeltaTime(IBinarySerializer serializer, ref DeltaTime v)
        {
            var tempMs = v.MilliSeconds;
            serializer.Add(ref tempMs);
        }

        public static void WriteDeltaTime(this IDataWriter serializer, ref DeltaTime v)
        {
            var tempMs = v.MilliSeconds;
            serializer.Add(ref tempMs);
        }

        private static DeltaTime ReadDeltaTime(IBinarySerializer serializer)
        {
            int tempInt = 0;
            serializer.Add(ref tempInt);
            return DeltaTime.FromMiliseconds(tempInt);
        }

        public static DeltaTime ReadDeltaTime(this IDataReader serializer)
        {
            int tempInt = 0;
            serializer.Add(ref tempInt);
            return DeltaTime.FromMiliseconds(tempInt);
        }
    }
}
