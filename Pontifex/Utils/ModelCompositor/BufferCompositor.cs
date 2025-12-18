using System;
using Actuarius.Memory;

namespace Pontifex.Utils
{
    public class BufferCompositor : IDisposable
    {
        private readonly IPool<IMultiRefByteArray, int> _arrayPool;
        
        private readonly int _maxPacketSize;
        private readonly BufferProcessorDelegate _bufferProcessor;
        
        private UnionDataMemoryAlias _packetSize = 0;
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

        public BufferCompositor(BufferProcessorDelegate bufferProcessor, IConcurrentPool<IMultiRefByteArray, int> arrayPool, int maxPacketSize = 1024 * 1024)
        {
            _arrayPool = arrayPool;
            _maxPacketSize = maxPacketSize;
            _bufferProcessor =  bufferProcessor;
        }

        public void Dispose()
        {
            _packetData?.Release();
            _packetData = null!;
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
                            _packetSize[_copiedCount] = bytes[start];
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
                        if (_packetSize.IntValue > _maxPacketSize)
                        {
                            throw new Exception($"ModelCompositor: packet size is too large ({_packetSize.IntValue})");
                        }
                        
                        _packetData = _arrayPool.Acquire(_packetSize.IntValue);
                        _copiedCount = 0;
                        _stage = Stage.FillBuffer;
                        break;
                    }
                    case Stage.FillBuffer:
                    {
                        int bytesToCopy = Math.Min(_packetSize.IntValue - _copiedCount, count);
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

                        if (_copiedCount == _packetSize.IntValue)
                        {
                            try
                            {
                                _bufferProcessor(_packetData!);
                            }
                            finally
                            {
                                _copiedCount = 0;
                                _packetData = null;
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