using System;
using System.Threading;
using FileLogConsumer;
using Shared;
using Shared.Utils;

namespace LogConsumers
{
    public abstract class BaseFileLogConsumer : Log.ILogConsumer
    {
        private readonly IPool<BytesBuffer> _bytesBufferPool;
        private readonly IPeriodicLogicRunner _logicRunner;
        private int _refCount = 1;

        private readonly BytesBuffer _accumulator;
        private readonly FileFlusher _fileFlusher;

        protected BaseFileLogConsumer(
            IPeriodicLogicRunner logicRunner,
            DeltaTime flushInterval,
            string fileName,
            IPool<BytesBuffer> bytesBufferPool)
            : this(logicRunner, flushInterval, fileName, 0, false, false, bytesBufferPool)
        {
        }

        protected BaseFileLogConsumer(
            IPeriodicLogicRunner logicRunner,
            DeltaTime flushInterval,
            string fileName,
            long maxSize,
            bool splitByDate,
            bool splitByRun,
            IPool<BytesBuffer> bytesBufferPool,
            Action<Exception> onError = null)
        {
            _logicRunner = logicRunner;

            _bytesBufferPool = bytesBufferPool;
            _accumulator = bytesBufferPool.Acquire();
            _fileFlusher = new FileFlusher(fileName, maxSize, splitByDate, splitByRun, _accumulator, onError);

            _logicRunner.Run(_fileFlusher, flushInterval);
        }

        public void Message(Log.MessageData logMessage)
        {
            _accumulator.Write(logMessage);
        }

        public void AddRef()
        {
            Interlocked.Increment(ref _refCount);
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _refCount) == 0)
            {
                _fileFlusher.Stop();

                _accumulator.Clear();
                _bytesBufferPool.Release(_accumulator);
            }
        }
    }
}