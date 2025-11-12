using System.Text;
using Shared;

namespace TransportAnalyzer.TestLogic
{
    class AckRawCommonLogic
    {
        const int MinN = 200;
        const int MaxN = 2000;

        protected static readonly byte[] AckResponse = Encoding.UTF8.GetBytes("STRESS-LOGIC-ACK-OK");

        public ILogger Log;

        protected AckRawCommonLogic()
        {
            Log = global::Log.StaticLogger;
        }

        public static byte[] GenBuffer(long id)
        {
            int N = (int)((id + 10) % MaxN + MinN);
            //N = 61;
            byte[] buffer = new byte[N];
            for (int i = 0; i < buffer.Length; ++i)
            {
                buffer[i] = (byte)((id + i) % 256);
            }
            return buffer;
        }

        public static bool CheckBuffer(long id, ByteArraySegment buffer)
        {
            int N = (int)(id + 10) % MaxN + MinN;
            //N = 61;
            if (buffer.Count != N)
            {
                return false;
            }

            for (int i = 0; i < N; ++i)
            {
                if (buffer[N - i - 1] != (byte)((id + i) % 256))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
