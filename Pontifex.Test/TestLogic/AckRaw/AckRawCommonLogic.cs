using System.Text;
using Actuarius.Memory;
using Scriba;

namespace TransportAnalyzer.TestLogic
{
    class AckRawCommonLogic
    {
        const int MinN = 200;
        const int MaxN = 2000;

        public static readonly IMultiRefReadOnlyByteArray AckRequest = new StaticReadOnlyByteArray("STRESS-LOGIC-ACK-REQUEST"u8.ToArray());
        public static readonly IMultiRefReadOnlyByteArray AckResponse = new StaticReadOnlyByteArray("STRESS-LOGIC-ACK-OK"u8.ToArray());

        public ILogger Log { get; private set; }
        public IMemoryRental Memory { get; private set; }

        protected AckRawCommonLogic()
        {
            Log = StaticLogger.Instance;
            Memory = MemoryRental.Shared;
        }
        
        public void Setup(IMemoryRental memory, ILogger logger)
        {
            Log = logger;
            Memory = memory;
        }

        protected IMultiRefByteArray GenBuffer(long id)
        {
            int N = (int)((id + 10) % MaxN + MinN);
            var buffer = Memory.ByteArraysPool.Acquire(N);
            //N = 61;
            for (int i = 0; i < N; ++i)
            {
                buffer[i] = (byte)((id + i) % 256);
            }
            return buffer;
        }

        protected static bool CheckBuffer(long id, IMultiRefReadOnlyByteArray buffer)
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
