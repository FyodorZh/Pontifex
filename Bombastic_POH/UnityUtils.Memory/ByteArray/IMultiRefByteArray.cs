using System;
using Actuarius.Memory;

namespace Shared
{
    public static class Ext_IMultiRefByteArray
    {
        public static MultiRefByteArrayOwner<TArray> Own<TArray>(this TArray array)
            where TArray : class, IMultiRefResource, IReadOnlyBytes
        {
            return new MultiRefByteArrayOwner<TArray>(array);
        }
    }

    public struct MultiRefByteArrayOwner<TArray> : IDisposable
        where TArray : class, IMultiRefResource, IReadOnlyBytes
    {
        private TArray mArray;

        public MultiRefByteArrayOwner(TArray array)
        {
            mArray = array;
        }

        public TArray Array
        {
            get { return mArray; }
        }

        public void Dispose()
        {
            if (mArray != null)
            {
                mArray.Release();
                mArray = null;
            }
        }
    }
}