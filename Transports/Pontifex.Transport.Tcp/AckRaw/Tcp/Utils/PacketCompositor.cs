using System;

namespace Pontifex.Transports.Tcp
{
    public class PacketCompositor
    {
        private struct BytesAccumulator
        {
            private readonly IMultiRefLowLevelByteArray mBuffer;

            private readonly byte[] mData;
            private readonly int mEndPosition;

            private int mPosition;

            public IMultiRefLowLevelByteArray Buffer { get { return mBuffer; } }

            public BytesAccumulator(int size)
            {
                mBuffer = CollectableByteArraySegmentWrapper.Construct(size);
                mData = mBuffer.ReadOnlyArray;
                mEndPosition = mBuffer.Offset + mBuffer.Count;
                mPosition = mBuffer.Offset;
            }

            public bool IsFull
            {
                get { return mPosition == mEndPosition; }
            }

            public void Append(byte[] bytes, int finalPosition, ref int position)
            {
                int bytesToWrite = mEndPosition - mPosition;
                int bytesToRead = finalPosition - position;

                int count = Math.Min(bytesToRead, bytesToWrite);
                if (count != 0)
                {
                    System.Buffer.BlockCopy(bytes, position, mData, mPosition, count);
                    position += count;
                    mPosition += count;
                }
            }
        }

        private readonly int mMaxMessageSize;
        private readonly BytesAccumulator mLengthAccumulator = new BytesAccumulator(sizeof(int));

        private bool mReadSizeMode;
        private BytesAccumulator mCurrentPacket;

        public PacketCompositor(int maxMessageSize = 1024 * 1024)
        {
            mMaxMessageSize = maxMessageSize;

            mReadSizeMode = true;
            mCurrentPacket = mLengthAccumulator;
        }

        public void Destroy()
        {
            var array1 = mCurrentPacket.Buffer;
            var array2 = mLengthAccumulator.Buffer;

            if (ReferenceEquals(array1, array2))
            {
                array2 = null;
            }

            if (array1 != null)
            {
                array1.Release();
            }

            if (array2 != null)
            {
                array2.Release();
            }
        }

        public void DecodePackets(byte[] data, int offset, int count, Action<Packet> onDecoded)
        {
            int position = offset;
            int finalPosition = offset + count;
            while (position < finalPosition)
            {
                mCurrentPacket.Append(data, finalPosition, ref position);

                if (mCurrentPacket.IsFull)
                {
                    if (mReadSizeMode)
                    {
                        int tmpOffset = mCurrentPacket.Buffer.Offset;
                        uint size = ReadUInt(mCurrentPacket.Buffer.ReadOnlyArray, ref tmpOffset);
                        if (size > 0 && size <= mMaxMessageSize)
                        {
                            mReadSizeMode = false;
                            mCurrentPacket = new BytesAccumulator((int)size);
                        }
                        else
                        {
                            throw new InvalidOperationException("Invalid message size = " + size);
                        }
                    }
                    else
                    {
                        var bytesArray = mCurrentPacket.Buffer;

                        PacketType type = (PacketType)bytesArray[0];

                        IMemoryBufferHolder buffer;
                        using (var subArrayHolder = bytesArray.Sub(1, bytesArray.Count - 1).Own())
                        {
                            if (!ConcurrentUsageMemoryBufferPool.Instance.AllocateAndDeserialize(subArrayHolder.Array, out buffer))
                            {
                                type = PacketType.Invalid;
                                buffer = ConcurrentUsageMemoryBufferPool.Instance.Allocate();
                            }
                        }

                        mCurrentPacket.Buffer.Release();
                        mCurrentPacket = mLengthAccumulator;
                        mReadSizeMode = true;

                        onDecoded(new Packet(type, buffer));
                    }
                }
            }
        }

        /// <summary>
        /// Тредобезопасно!
        /// </summary>
        public static int EncodePacketTo(Packet packet, ref byte[] buffer)
        {
            using (var bufferAccessor = packet.Buffer.ExposeAccessorOnce())
            {
                int bufferSize = bufferAccessor.Buffer.Size;

                int requiredDataLength;
                {
                    requiredDataLength = sizeof(int) + 1 + bufferSize;
                    int bufferLength = buffer.Length;
                    while (bufferLength < requiredDataLength)
                    {
                        bufferLength = (bufferLength + 1) * 2;
                    }
                    if (bufferLength != buffer.Length)
                    {
                        buffer = new byte[bufferLength];
                    }
                }

                int offset = 0;
                WriteUInt((uint)(bufferSize + 1), buffer, ref offset);
                buffer[offset++] = (byte)packet.Type;
                var sink = ByteArraySink.ThreadInstance(buffer, offset);
                bufferAccessor.Buffer.TryWriteTo(sink);

                return requiredDataLength;
            }
        }

        private static uint ReadUInt(byte[] src, ref int offset)
        {
            uint res = src[offset++];
            res += (uint)src[offset++] << 8;
            res += (uint)src[offset++] << 16;
            res += (uint)src[offset++] << 24;
            return res;
        }

        private static void WriteUInt(uint value, byte[] dst, ref int offset)
        {
            dst[offset++] = (byte)((value >> 0) & 0xFF);
            dst[offset++] = (byte)((value >> 8) & 0xFF);
            dst[offset++] = (byte)((value >> 16) & 0xFF);
            dst[offset++] = (byte)((value >> 24) & 0xFF);
        }
    }
}