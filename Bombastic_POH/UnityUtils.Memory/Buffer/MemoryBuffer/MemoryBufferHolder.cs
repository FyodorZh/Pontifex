using System;
using Shared.ByteSinks;

namespace Shared.Buffer
{
    public sealed class MemoryBufferHolder : PoolObjectSafeMultiUser<IMemoryBuffer>, IMemoryBufferAccessor, IMemoryBufferHolder
    {
        public MemoryBufferHolder(IMemoryBufferPool owner, IMemoryBuffer buffer)
            : base(owner, buffer)
        {
            Revive();
        }

        protected override void OnReleased()
        {
            mObject.Clear();
            base.OnReleased();
        }

        protected override void OnRefCountError(ErrorType error)
        {
            if (error != ErrorType.Leak)
            {
                ((IMemoryBufferPool)mPool).Log.e("Wrong MemoryBuffer usage: '{0}'", error);
                base.OnRefCountError(error);
            }
            else
            {
                ((IMemoryBufferPool)mPool).Log.w("MemoryBuffer leak detected");
                base.OnRefCountError(error);
            }
        }

        void IDisposable.Dispose()
        {
            Release();
        }

        IMemoryBufferHolder IMemoryBufferAccessor.Acquire()
        {
            AddRef();
            return this;
        }

        IMemoryBuffer IMemoryBufferAccessor.Buffer
        {
            get { return mObject; }
        }

        IMemoryBufferHolder IMemoryBufferHolder.Acquire()
        {
            AddRef();
            return this;
        }

        IMemoryBuffer IMemoryBufferHolder.ShowBufferUnsafe()
        {
            return mObject;
        }

        IMemoryBufferAccessor IMemoryBufferHolder.ExposeAccessorOnce()
        {
            return this;
        }

        bool IMemoryBufferHolder.Validate()
        {
            using (var accessor = ((IMemoryBufferHolder)this).Acquire().ExposeAccessorOnce())
            {
                var buffer = accessor.Buffer;
                if (buffer != null)
                {
                    return buffer.Validate();
                }
                return false;
            }
        }

        public override string ToString() // небезопасный метод :(
        {
            var buffer = mObject;
            if (buffer == null)
            {
                return "null";
            }
            return buffer.ToString();
        }

        int IByteArray.Count
        {
            get
            {
                var buffer = mObject;
                return buffer != null ? buffer.Size : 0;
            }
        }

        bool IByteArray.IsValid
        {
            get
            {
                return mObject != null;
            }
        }

        bool IByteArray.CopyTo(byte[] dst, int dstOffset, int srcOffset, int count)
        {
            var buffer = mObject;
            if (buffer != null)
            {
                int byteSize = buffer.Size;

                if (dst == null || dstOffset < 0 || srcOffset < 0 || count < 0 ||
                    dstOffset + count > dst.Length || srcOffset + count > byteSize)
                {
                    return false;
                }

                var sink = RangedByteArraySink.ThreadInstance(srcOffset, dst, dstOffset, count);
                return buffer.TryWriteTo(sink);
            }

            return false;
        }
    }
}
