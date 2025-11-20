#if UNITY_EDITOR
#define SELF_CHECK
//#define DEEP_CHECK
//#define PARANOIC_CHECK
#else // SERVER
#if DEBUG
        //#define SELF_CHECK
        //#define DEEP_CHECK
        //#define PARANOIC_CHECK
#endif
#endif

// SELF_CHECK       = подсчёт ссылок, контроль утечек в деструкторе
// DEEP_CHECK       = SELF_CHECK + логирование стеков аллкации объекта
// PARANOIC_CHECK   = DEEP_CHECK + логироание стеков AddRef()

using System;
using Actuarius.Memory;

namespace Shared
{
    /// Обёртка над массивом байт.
    /// Поддерживает:
    /// 1. AddRef() / Release() с подсчётом ссылок
    /// 2. Автоматическую аллокацию/освобождение данных из пула
    /// 3. Отслеживает утечки памяти и некорректное использование(в режиме отладки)
    public class ByteArray : IDisposable, Pool.ICollectable, ISingleRefResource
    {
        private Pool.IObjectPool mOwner;
        private int mRefCount;
        private int mLength;
        private byte[] mData;
        private bool mDataFromPool;

        public int RefCount { get { return mRefCount; } }

#if DEEP_CHECK
        private static readonly System.Reflection.MethodBase mAllocatorMethod =
            typeof(Pool.ObjectPool<ByteArray>).GetMethod("Allocate", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        [ThreadStatic]
        private static System.Text.StringBuilder mStringBuilder;

        private System.Diagnostics.StackTrace mCallStack;

        public readonly int mContextId;

#if PARANOIC_CHECK
        private ByteArray mAddRefSource;
#endif
#endif

        /// <summary>
        /// Аллоцирует данные заданной длины (использует пул)
        /// </summary>
        /// <param name="length"></param>
        public static ByteArray Allocate(int length)
        {
            return Construct(Pool.BytesPool.Allocate(length), length, true);
        }

        /// <summary>
        /// Аллоцирует данные не менее чем заданной длины (использует пул)
        /// </summary>
        /// <param name="length"></param>
        public static ByteArray AllocateNoLess(int length)
        {
            return Construct(Pool.BytesPool.AllocateNoLess(length), length, true);
        }

        /// <summary>
        /// Аллоцирует копию предоставленных данных (использует пул)
        /// </summary>
        /// <param name="srcBytes"> Источник данных </param>
        /// <param name="fromPos"> С какого момента копировать </param>
        /// <param name="count"> Размер копируемых данных </param>
        public static ByteArray Allocate(byte[] srcBytes, int fromPos, int count)
        {
            ByteArray array = Construct(Pool.BytesPool.Allocate(count), count, true);
            System.Buffer.BlockCopy(srcBytes, fromPos, array.Data, 0, count);
            return array;
        }

        /// <summary>
        /// Берёт предоставленные данные под свой контроль. Новые данные не аллоцируются.
        /// </summary>
        public static ByteArray AssumeControl(byte[] data)
        {
            return Construct(data, data.Length, false);
        }

        /// <summary>
        /// Берёт данные под свой контроль. Новые данные не аллоцируются
        /// </summary>
        /// <param name="data"> Данные </param>
        /// <param name="length"> Эффективная длина данных </param>
        /// <param name="returnDataToPool"> Необходимо ли вернуть данные в пул после использования </param>
        public static ByteArray AssumeControl(byte[] data, int length, bool returnDataToPool)
        {
            return Construct(data, length, returnDataToPool);
        }

        private static ByteArray Construct(byte[] data, int length, bool returnDataToPool)
        {
            ByteArray array = Pool.ObjectPool<ByteArray>.Allocate();

            array.mLength = length;
            array.mData = data;
            array.mDataFromPool = returnDataToPool;
            array.mRefCount = 1;
            return array;
        }

        public ByteArray()
        {
            mRefCount = 1;

#if DEEP_CHECK
            mContextId = SingletonsManager.ContextId;
            if (mContextId == 0)
            {
                Log.w("Signeltons context == 0");
            }
#endif
        }

        /// <summary>
        /// Assume control NO POOL
        /// </summary>
        public ByteArray(byte[] data, int length)
        {
            mLength = length;
            mData = data;
            mDataFromPool = false;
            mRefCount = 1;
        }

        public int Length
        {
            get
            {
                return mLength;
            }
        }

        /// <summary>
        /// Доступ к данным без права владения
        /// Data.Length != this.Length
        /// </summary>
        public byte[] Data
        {
            get
            {
#if SELF_CHECK
                DBG.Diagnostics.Assert(mRefCount > 0);
#endif
                return mData;
            }
        }

        /// <summary>
        /// Создаёт копию хранимых данных
        /// </summary>
        /// <returns></returns>
        public byte[] DataCopy()
        {
#if SELF_CHECK
            DBG.Diagnostics.Assert(mRefCount > 0);
#endif
            byte[] bytes = new byte[mLength];
            System.Buffer.BlockCopy(mData, 0, bytes, 0, mLength);
            return bytes;
        }

        /// <summary>
        /// Создаёт копию хранимых данных
        /// </summary>
        /// <returns></returns>
        public byte[] DataCopyFromPool()
        {
#if SELF_CHECK
            DBG.Diagnostics.Assert(mRefCount > 0);
#endif
            byte[] bytes = Pool.BytesPool.Allocate(mLength);
            System.Buffer.BlockCopy(mData, 0, bytes, 0, mLength);
            return bytes;
        }

        public ByteArray AddRef()
        {
#if PARANOIC_CHECK
            ContextCheck();
#endif
            int refCount = ++mRefCount;

            if (refCount == 1)
            {
#if DEEP_CHECK
                Log.e("AddRef ByteArray when mRefCount = {0}, allocated from\n{1}", refCount - 1, CallStackText());
#else
                Log.e("AddRef ByteArray when mRefCount = {0}", refCount - 1);
#endif
            }

#if PARANOIC_CHECK
            ByteArray ret = Construct(mData, mData.Length, false);
            ret.mAddRefSource = this;
            return ret;
#else
            return this;
#endif

        }

        public void Release()
        {
#if DEEP_CHECK
            ContextCheck();
#endif

            int refCount = --mRefCount;

            if (refCount == -1)
            {
#if DEEP_CHECK
                Log.e("Release ByteArray when mRefCount = {0}, allocated from\n{1}", refCount + 1, CallStackText());
#else
                Log.e("Release ByteArray when mRefCount = {0}", refCount + 1);
#endif
            }

            if (refCount == 0)
            {
#if PARANOIC_CHECK
                if (mAddRefSource != null)
                {
                    mAddRefSource.Release();
                    mAddRefSource = null;
                }
#endif

                var obj = this;
                Pool.ObjectPool<ByteArray>.Free(ref obj);
            }
        }

        public void Dispose()
        {
            Release();
        }

        void Pool.ICollectable.Initialize(Pool.IObjectPool owner)
        {
            mOwner = owner;
#if DEEP_CHECK
            mCallStack = new System.Diagnostics.StackTrace(true);
#endif
        }

        void Pool.ICollectable.Collect()
        {
            DBG.Diagnostics.Assert(mRefCount == 0);
            if (mDataFromPool)
            {
                Pool.BytesPool.Free(ref mData);
            }
            mData = null;

#if PARANOIC_CHECK
            ContextCheck();
#endif
        }

        void Pool.ICollectable.Restore()
        {
            DBG.Diagnostics.Assert(mRefCount == 0);
#if DEEP_CHECK
            mCallStack = new System.Diagnostics.StackTrace(true);
#endif

#if PARANOIC_CHECK
            ContextCheck();
#endif
        }

        Pool.IObjectPool Pool.ICollectable.Owner
        {
            get { return mOwner; }
        }

#if SELF_CHECK
        //~ByteArray()
        //{
        //    if (mRefCount != 0)
        //    {
        //    #if DEEP_CHECK
        //        Log.w("ByteArray Leak({0}) detected, allocated from\n{1}", mRefCount, CallStackText());
        //    #else
        //        Log.w("ByteArray Leak({0}) detected", mRefCount);
        //    #endif
        //    }
        //}

#if DEEP_CHECK
        private string CallStackText()
        {
            int depth = 0;
            for (int i = mCallStack.FrameCount - 1; i >= 0; --i)
            {
                if (mCallStack.GetFrame(i).GetMethod() == mAllocatorMethod)
                {
                    depth = i + 3;
                    break;
                }
            }

            if (mStringBuilder == null)
            {
                mStringBuilder = new System.Text.StringBuilder();
            }
            mStringBuilder.Length = 0;
            for (int i = depth; i < mCallStack.FrameCount; ++i)
            {
                var frame = mCallStack.GetFrame(i);
                mStringBuilder.Append(frame.GetMethod());
                mStringBuilder.Append(" ");
                mStringBuilder.Append(frame.GetFileName());
                mStringBuilder.Append(":");
                mStringBuilder.Append(frame.GetFileLineNumber());
                mStringBuilder.AppendLine();
            }

            var str = mStringBuilder.ToString();
            return str;
        }

        private void ContextCheck()
        {
            if (mContextId != SingletonsManager.ContextId)
            {
                Log.e("Context check failed. expected context #{0} but actual context is #{1}", mContextId, SingletonsManager.ContextId);
            }
        }
#endif
#endif
    }
}