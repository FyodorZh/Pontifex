using System;
using Shared.ByteSinks;

namespace Shared.Buffer
{
    public interface IMemoryBufferHolder : IMultiRefByteArray
    {
        /// <summary>
        /// Показывает буфер без права владения.
        /// Безопасно использовать метод можно только в случае если BufferHolder уже находится во влвдении
        /// и будет продолжать находиться в нём на протяжении всего периода использования IMemoryBuffer
        /// </summary>
        /// <returns></returns>
        IMemoryBuffer ShowBufferUnsafe();

        IMemoryBufferHolder Acquire();

        IMemoryBufferAccessor ExposeAccessorOnce();

        bool Validate();
    }

    public interface IMemoryBufferAccessor : IDisposable
    {
        IMemoryBufferHolder Acquire();
        IMemoryBuffer Buffer { get; }
    }

    public interface IMemoryBufferView
    {
        IDLong<IMemoryBuffer> BufferId { get; }

        /// <summary>
        /// Memory size
        /// </summary>
        int Size { get; }
        bool TryWriteTo(IByteSink sink);

        /// <summary>
        /// Number of elements
        /// </summary>
        int Count { get; }
    }

    public interface IMemoryBufferPusher : IMemoryBufferView
    {
        void PushArray(ByteArraySegment bytes, bool before = true);
        void PushAbstractArray(IMultiRefByteArray bytes, bool before = true);
        void PushBuffer(IMemoryBufferHolder buffer, bool before = true);
        void PushBoolean(Boolean value, bool before = true);
        void PushByte(Byte value, bool before = true);
        void PushUInt16(UInt16 value, bool before = true);
        void PushInt32(Int32 value, bool before = true);
        void PushInt64(Int64 value, bool before = true);
        void PushSingle(Single value, bool before = true);
        void PushDouble(Double value, bool before = true);
    }

    public interface IMemoryBufferPoper : IMemoryBufferView
    {
        BufferElement PopFirst();
    }

    public interface IMemoryBuffer : IMemoryBufferPusher, IMemoryBufferPoper
    {
        bool Validate();
        bool ReadFrom<TBytes>(TBytes data) where TBytes : IMultiRefLowLevelByteArray;
        bool CloneFrom(IMemoryBufferHolder buffer);
        void Clear();
    }

    public static class Ext_IMemoryBuffer
    {
        public static byte[] ToArray(this IMemoryBuffer buffer)
        {
            var array = new byte[buffer.Size];
            var sink = ByteArraySink.ThreadInstance(array);

            if (!buffer.TryWriteTo(sink))
            {
                return null;
            }
            return array;
        }
    }
}