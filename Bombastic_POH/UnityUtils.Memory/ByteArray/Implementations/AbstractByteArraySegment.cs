using System;
using Actuarius.Memory;

namespace Shared
{
    /// <summary>
    /// Тривиальная обёртка над массивом.
    /// </summary>
    /// <typeparam name="TArray"></typeparam>
    public class AbstractByteArraySegment<TArray> : MultiRefImpl, IMultiRefByteArray
        where TArray : IReadOnlyBytes, IReleasableResource
    {
        private readonly TArray mArray;

        public AbstractByteArraySegment(TArray array)
            : base(false)
        {
            mArray = array;
        }

        protected override void OnReleased()
        {
            mArray.Release();
        }

        public int Count
        {
            get { return mArray.Count; }
        }

        public bool IsValid
        {
            get { return mArray.IsValid; }
        }

        public bool CopyTo(byte[] dst, int dstOffset, int srcOffset, int count)
        {
            return mArray.CopyTo(dst, dstOffset, srcOffset, count);
        }
    }
}