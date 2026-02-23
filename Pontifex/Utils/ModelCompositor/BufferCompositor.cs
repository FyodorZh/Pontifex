using System;
using System.Threading;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Utils
{
    public class BufferCompositor : IDisposable
    {
        private readonly IPool<IMultiRefByteArray, int> _arrayPool;
        
        private readonly int _maxMessageSize;
        private readonly BufferProcessorDelegate _bufferProcessor;
        
        private UnionDataMemoryAlias _messageSize = 0;
        private IMultiRefByteArray? _packetData;
        
        private int _copiedCount;

        private Stage _stage = Stage.FillSize;

        private enum Stage
        {
            FillSize,
            AllocateBuffer,
            FillBuffer
        }

        public delegate void BufferProcessorDelegate(IMultiRefByteArray bytes);

        public BufferCompositor(BufferProcessorDelegate bufferProcessor, IConcurrentPool<IMultiRefByteArray, int> arrayPool, int maxMessageSize)
        {
            _arrayPool = arrayPool;
            _maxMessageSize = maxMessageSize;
            _bufferProcessor =  bufferProcessor;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _packetData, null)?.Release();
        }
        
        public void PushData(byte[] bytes, int start, int count)
        {
            while (true)
            {
                switch (_stage)
                {
                    case Stage.FillSize:
                    {
                        int bytesToCopy = Math.Min(4 - _copiedCount, count);
                        for (int i = 0; i < bytesToCopy; i++)
                        {
                            _messageSize[_copiedCount] = bytes[start];
                            _copiedCount += 1;
                            start += 1;
                            count -= 1;
                        }

                        if (_copiedCount == 4)
                        {
                            _stage = Stage.AllocateBuffer;
                            break;
                        }

                        return;
                    }
                    case Stage.AllocateBuffer:
                    {
                        if (_messageSize.IntValue > _maxMessageSize)
                        {
                            throw new Exception($"ModelCompositor: packet size is too large ({_messageSize.IntValue})");
                        }

                        var packet = Interlocked.Exchange(ref _packetData, _arrayPool.Acquire(_messageSize.IntValue));
                        if (packet != null)
                        {
                            throw new Exception();
                        }
                        
                        _copiedCount = 0;
                        _stage = Stage.FillBuffer;
                        break;
                    }
                    case Stage.FillBuffer:
                    {
                        int bytesToCopy = Math.Min(_messageSize.IntValue - _copiedCount, count);
                        if (bytesToCopy > 0)
                        {
                            if (!_packetData?.CopyFrom(bytes, start, _copiedCount, bytesToCopy) ?? false)
                            {
                                throw new Exception();
                            }
                            _copiedCount += bytesToCopy;
                            count -= bytesToCopy;
                            start += bytesToCopy;
                        }

                        if (_copiedCount == _messageSize.IntValue)
                        {
                            try
                            {
                                var packet = Interlocked.Exchange(ref _packetData, null);
                                _bufferProcessor(packet ?? throw new Exception());
                            }
                            finally
                            {
                                _copiedCount = 0;
                                _stage = Stage.FillSize;
                            }
                            break;
                        }

                        return;
                    }
                }
            }
        }
    }
}