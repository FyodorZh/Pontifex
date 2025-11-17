using System;
using Actuarius.Memoria;

namespace Shared
{
    /// <summary>
    /// Абстрактное тредобезопасное хранилище последовательности байт с контролем владения.
    /// Обещает быть иммутабельной!!!
    /// </summary>
    public interface IMultiRefByteArray : IByteArray, IMultiRef
    {
    }

    public static class Ext_IMultiRefByteArray
    {
        public static MultiRefByteArrayOwner<TArray> Own<TArray>(this TArray array)
            where TArray : class, IMultiRef, IByteArray
        {
            return new MultiRefByteArrayOwner<TArray>(array);
        }
    }

    public struct MultiRefByteArrayOwner<TArray> : IDisposable
        where TArray : class, IMultiRef, IByteArray
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