using System;
using System.IO;
using Shared;
using Shared.Utils;

namespace LogConsumers
{
    public class RawJsonFileLogConsumer : BaseFileLogConsumer
    {
        public RawJsonFileLogConsumer(IPeriodicLogicRunner logicRunner, DeltaTime flushInterval, string fileName, IPool<BytesBuffer> bytesBufferPool)
            : base(logicRunner, flushInterval, fileName, bytesBufferPool)
        {
        }

        public RawJsonFileLogConsumer(IPeriodicLogicRunner logicRunner, DeltaTime flushInterval, string fileName, long maxSize, bool splitByDate, bool splitByRun, IPool<BytesBuffer> bytesBufferPool, Action<Exception> onError = null)
            : base(logicRunner, flushInterval, fileName, maxSize, splitByDate, splitByRun, bytesBufferPool, onError)
        {
        }
    }
}