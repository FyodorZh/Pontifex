using System;

namespace Shared.Buffer
{
    public class MemoryBuffer : IMemoryBuffer
    {
        private static long mIdCounter = 0;

        private readonly long mId = System.Threading.Interlocked.Increment(ref mIdCounter);
        private readonly CycleQueue<BufferElement> mElements = new CycleQueue<BufferElement>(10);

        private IMultiRefLowLevelByteArray mSource;

        public long BufferId
        {
            get { return mId; }
        }

        public int Size
        {
            get
            {
                int size = 0;
                foreach (var element in mElements.Enumerate(QueueEnumerationOrder.HeadToTail))
                {
                    size += element.Size;
                }
                return size;
            }
        }

        public bool TryWriteTo(IByteSink sink)
        {
            foreach (var element in mElements.Enumerate(QueueEnumerationOrder.HeadToTail))
            {
                if (!element.TryWriteTo(sink))
                {
                    return false;
                }
            }
            return true;
        }

        public bool Validate()
        {
            foreach (var element in mElements.Enumerate())
            {
                if (element.Type == BufferElementType.Unknown)
                {
                    return false;
                }
                if (element.Type == BufferElementType.Buffer)
                {
                    IMemoryBufferHolder buffer;
                    element.AsBuffer(out buffer);
                    if (!buffer.Validate())
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public bool ReadFrom<TBytes>(TBytes data)
            where TBytes : IMultiRefLowLevelByteArray
        {
            if (mSource != null || mElements.Count > 0)
            {
                Clear();
            }

            try
            {
                int count = data.Count;
                int offset = data.Offset;
                int maxOffset = offset + count;
                byte[] array = data.Array;

                if (!(data is ByteArraySegment)) // оптимизация боксинга
                {
                    data.AddRef();
                    mSource = data;
                }
                else
                {
                    mSource = null;
                }

                while (offset < maxOffset)
                {
                    BufferElement element = new BufferElement(array, ref offset, this);
                    if (offset > maxOffset || element.Type == BufferElementType.Unknown)
                    {
                        element.Clear();
                        Clear();
                        return false;
                    }
                    mElements.Put(element);
                }

                return true;
            }
            catch
            {
                Clear();
                return false;
            }
        }

        internal IMultiRefLowLevelByteArray Source
        {
            get { return mSource; }
        }

        internal int SourceBaseOffset
        {
            get { return mSource.Offset; }
        }

        public bool CloneFrom(IMemoryBufferHolder buffer)
        {
            Clear();
            using (var bufferAccessor = buffer.Acquire().ExposeAccessorOnce())
            {
                MemoryBuffer src = (MemoryBuffer)bufferAccessor.Buffer;
                foreach (var element in src.mElements.Enumerate())
                {
                    Push(element.Clone(), false);
                }

                if (src.mSource != null)
                {
                    mSource = src.mSource.Acquire();
                }

                return true;
            }
        }

        public int Count
        {
            get { return mElements.Count; }
        }

        public BufferElement PopFirst()
        {
            BufferElement result;
            mElements.TryPop(out result);
            return result;
        }

        public void Clear()
        {
            foreach (var element in mElements.Enumerate())
            {
                element.Clear();
            }
            mElements.Clear();
            if (mSource != null)
            {
                mSource.Release();
                mSource = null;
            }
        }

        public void PushArray(ByteArraySegment bytes, bool before)
        {
            BufferElement element = new BufferElement(bytes);
            Push(element, before);
        }

        public void PushAbstractArray(IMultiRefByteArray bytes, bool before)
        {
            BufferElement element = new BufferElement(bytes);
            Push(element, before);
        }

        public void PushBoolean(Boolean value, bool before)
        {
            BufferElement element = new BufferElement(value);
            Push(element, before);
        }

        public void PushByte(Byte value, bool before)
        {
            BufferElement element = new BufferElement(value);
            Push(element, before);
        }

        public void PushUInt16(UInt16 value, bool before)
        {
            BufferElement element = new BufferElement(value);
            Push(element, before);
        }

        public void PushInt32(Int32 value, bool before)
        {
            BufferElement element = new BufferElement(value);
            Push(element, before);
        }

        public void PushInt64(Int64 value, bool before)
        {
            BufferElement element = new BufferElement(value);
            Push(element, before);
        }

        public void PushSingle(Single value, bool before)
        {
            BufferElement element = new BufferElement(value);
            Push(element, before);
        }

        public void PushDouble(Double value, bool before)
        {
            BufferElement element = new BufferElement(value);
            Push(element, before);
        }

        public void PushBuffer(IMemoryBufferHolder value, bool before)
        {
            BufferElement element = new BufferElement(value);
            Push(element, before);
        }

        private void Push(BufferElement element, bool before)
        {
            if (before)
            {
                mElements.EnqueueToHead(element);
            }
            else
            {
                mElements.Put(element);
            }
        }

        public override string ToString()
        {
            using (var sbAccessor = StringBuilderInstance.Get())
            {
                var sb = sbAccessor.SB;
                sb.AppendFormat("Buffer#{0}[{1}/{2}]{{", mId, mElements.Count, this.Size);

                bool bFirst = true;
                foreach (var element in mElements.Enumerate())
                {
                    if (!bFirst)
                    {
                        sb.Append("; ");
                    }
                    bFirst = false;

                    element.WriteTo(sb);
                }
                sb.Append("}");
                return sb.ToString();
            }
        }
    }
}
