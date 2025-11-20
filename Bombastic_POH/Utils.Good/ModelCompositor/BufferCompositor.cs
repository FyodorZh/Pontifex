using System;

namespace Archivarius.Tools
{
    public class BufferCompositor
    {
        private readonly int _maxPacketSize;
        private readonly BufferProcessorDelegate _bufferProcessor;
        
        private UnionDataMemoryAlias _packetSize = 0;
        private byte[] _packetData = Array.Empty<byte>();
        
        private int _copiedCount = 0;

        private Stage _stage = Stage.FillSize;

        private enum Stage
        {
            FillSize,
            AllocateBuffer,
            FillBuffer
        }

        public delegate void BufferProcessorDelegate(byte[] bytes, int count);

        public BufferCompositor(BufferProcessorDelegate bufferProcessor, int maxPacketSize = 1024 * 1024)
        {
            _maxPacketSize = maxPacketSize;
            _bufferProcessor =  bufferProcessor;
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
                        if (_packetData.Length < _packetSize.IntValue)
                        {
                            _packetData = new byte[_packetSize.IntValue * 2];
                        }
                        _copiedCount = 0;
                        _stage = Stage.FillBuffer;
                        break;
                    }
                    case Stage.FillBuffer:
                    {
                        int bytesToCopy = Math.Min(_packetSize.IntValue - _copiedCount, count);
                        if (bytesToCopy > 0)
                        {
                            Buffer.BlockCopy(bytes, start, _packetData, _copiedCount, bytesToCopy);
                            _copiedCount += bytesToCopy;
                            count -= bytesToCopy;
                            start += bytesToCopy;
                        }

                        if (_copiedCount == _packetSize.IntValue)
                        {
                            try
                            {
                                _bufferProcessor(_packetData, _copiedCount);
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