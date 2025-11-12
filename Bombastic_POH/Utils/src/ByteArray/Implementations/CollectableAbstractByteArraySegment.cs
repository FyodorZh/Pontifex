using Shared.Pooling;

namespace Shared
{
    /// <summary>
    /// Обёртка над абстрактным массивом байт.
    /// Позволяет определить поддиапазон.
    /// Контроль владения и автоматическое пулирование
    /// </summary>
    public abstract class CollectableAbstractByteArraySegment<T, TByteArray> : MultiRefCollectable<T>, IMultiRefByteArray
        where TByteArray : class, IMultiRefByteArray
        where T : CollectableAbstractByteArraySegment<T, TByteArray>
    {
        protected TByteArray mCore;
        protected int mOffset;
        protected int mCount;

        /// <summary>
        /// Инициализирует сгмент над абстрактным сегментом байт
        /// </summary>
        /// <param name="core"> Опорный сегмент. Берётся во владение </param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns> В случае неуспеха вернёт невалидный сегмент </returns>
        public T Init(TByteArray core, int offset, int count)
        {
            if (core == null || !core.IsValid || offset < 0 || count < 0 || core.Count < offset + count)
            {
                mCore = null;
                mOffset = 0;
                mCount = 0;
                if (core != null)
                {
                    core.Release();
                }
            }
            else
            {
                mCore = core;
                mOffset = offset;
                mCount = count;
            }

            return (T)this;
        }

        protected override void OnCollected()
        {
            if (mCore != null)
            {
                mCore.Release();
                mCore = null;
            }

            mOffset = 0;
            mCount = 0;
        }

        protected override void OnRestored()
        {
            // DO NOTHING
        }

        public int Count
        {
            get { return mCount; }
        }

        public bool IsValid
        {
            get { return mCore != null && mCore.IsValid; }
        }

        public bool CopyTo(byte[] dst, int dstOffset, int srcOffset, int count)
        {
            var core = mCore;
            if (core == null)
            {
                return false;
            }
            if (count < 0 || srcOffset < 0 || srcOffset + count > mCount)
            {
                return false;
            }

            return core.CopyTo(dst, dstOffset, mOffset + srcOffset, count);
        }
    }

    public class CollectableAbstractByteArraySegment : CollectableAbstractByteArraySegment<CollectableAbstractByteArraySegment, IMultiRefByteArray>
    {
    }

    public class CollectableAbstractLowLevelByteArraySegment : CollectableAbstractByteArraySegment<CollectableAbstractLowLevelByteArraySegment, IMultiRefLowLevelByteArray>, IMultiRefLowLevelByteArray
    {
        public byte[] Array
        {
            get { return mCore.Array; }
        }

        public int Offset
        {
            get { return mCore.Offset + mOffset; }
        }

        public byte this[int id]
        {
            get { return mCore[mOffset + id]; }
        }
    }
}