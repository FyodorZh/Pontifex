using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Shared.ByteSinks;

namespace Shared.Buffer
{
    public class MemoryBufferAsHolder: MemoryBuffer, IMemoryBufferHolder, IMemoryBufferAccessor
    {
        private class MultiRefImpl1 : MultiRefResource
        {
            private readonly MemoryBufferAsHolder mOwner;
            public MultiRefImpl1(MemoryBufferAsHolder owner)
                : base(true)
            {
                mOwner = owner;
            }

            public new bool Revive()
            {
                return base.Revive();
            }

            protected override void OnReleased()
            {
                mOwner.DeInit();
            }
        }

        private readonly MultiRefImpl1 mRefHolder;

        private IPoolSink<IMemoryBuffer> mPool;

        public MemoryBufferAsHolder()
        {
            mRefHolder = new MultiRefImpl1(this);
        }

        public void Init(IPoolSink<IMemoryBuffer> pool)
        {
            mPool = pool;
            mRefHolder.Revive();
        }

        private void DeInit()
        {
            Clear();
            if (mPool != null)
            {
                mPool.Release(this);
                mPool = null;
            }
        }

        #region IMultiRef
        void IReleasableResource.Release()
        {
            mRefHolder.Release();
        }

        bool IMultiRefResource.IsAlive
        {
            get { return mRefHolder.IsAlive; }
        }

        void IMultiRefResource.AddRef()
        {
            mRefHolder.AddRef();
        }
        #endregion

        #region IMemoryBufferHolder
        IMemoryBufferAccessor IMemoryBufferHolder.ExposeAccessorOnce()
        {
            return this;
        }

        IMemoryBuffer IMemoryBufferHolder.ShowBufferUnsafe()
        {
            return this;
        }

        IMemoryBufferHolder IMemoryBufferHolder.Acquire()
        {
            mRefHolder.AddRef();
            return this;
        }
        #endregion

        #region IMemoryBufferAccessor
        IMemoryBuffer IMemoryBufferAccessor.Buffer
        {
            get { return this; }
        }

        void IDisposable.Dispose()
        {
            mRefHolder.Release();
        }

        IMemoryBufferHolder IMemoryBufferAccessor.Acquire()
        {
            mRefHolder.AddRef();
            return this;
        }
        #endregion

        #region IByteArray
        int ICountable.Count
        {
            get { return Size; }
        }

        bool IReadOnlyBytes.IsValid
        {
            get { return true; }
        }

        bool IReadOnlyBytes.CopyTo(byte[] dst, int dstOffset, int srcOffset, int count)
        {
            if (dst == null || dstOffset < 0 || srcOffset < 0 || count < 0 ||
                dstOffset + count > dst.Length || srcOffset + count > Size)
            {
                return false;
            }

            var sink = RangedByteArraySink.ThreadInstance(srcOffset, dst, dstOffset, count);
            return TryWriteTo(sink);
        }
        #endregion
    }
}