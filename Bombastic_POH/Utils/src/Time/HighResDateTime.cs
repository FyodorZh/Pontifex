using System;
using System.Runtime.InteropServices;

namespace Shared
{
    public static class HighResDateTime
    {
        public static bool IsAvailable { get; private set; }
#if UNITY && !UNITY_EDITOR_WIN
        private static void GetSystemTimePreciseAsFileTime(out long filetime)
        {
            throw new NotImplementedException();
        }
#else
        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern void GetSystemTimePreciseAsFileTime(out long filetime);
#endif

        public static DateTime UtcNow
        {
            get
            {
                if (IsAvailable)
                {
                    long filetime;
                    GetSystemTimePreciseAsFileTime(out filetime);
                    return DateTime.FromFileTimeUtc(filetime);
                }

                return DateTime.UtcNow;
            }
        }

        static HighResDateTime()
        {
            try
            {
                var os = Environment.OSVersion; 
                if (os.Platform == PlatformID.Win32NT)
                {
                    if (os.Version.Major > 6 || (os.Version.Major == 6 && os.Version.Minor >= 2)) // Windows 8 or higher.
                    {
                        long filetime;
                        GetSystemTimePreciseAsFileTime(out filetime);
                        IsAvailable = true;
                    }
                }
            }
            catch (Exception)
            {
                IsAvailable = false;
            }

            if (!IsAvailable)
            {
                Log.w("HighResoulutionDateTime is not supported!");
            }
        }
    }
}