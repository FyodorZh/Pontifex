//#define MEM_CHECK
#if UNITY_EDITOR
#define MEM_CHECK
#endif

namespace Shared.Pool
{
    /// <summary>
    /// Все объекты хранимые в ObjectPool должны реализовать этот интерфейс.
    /// Жизненный цикл объектов должен быть таким:
    /// 1. Сразу после вызова конструктора объект готов к использованию.
    /// 2. После освобождения объекта с помощью ObjectPool.Free() вызывается ICollectable.Collect(). Объект себя чистит и переходит в инвалидное состояние
    /// 3. После повторного аллоцирования объекту вызывется ICollectable.Restore(), в результате чего он подготавливается к работе и ведёт себя как после конструирования.
    /// </summary>
    public interface ICollectable
    {
        void Initialize(IObjectPool owner);
        /// <summary>
        /// Вызывается всякий раз при записи объекта в пулл. Объект может выгрузить часть своих данных
        /// </summary>
        void Collect();
        /// <summary>
        /// Вызывается при изъятии объекта из пула. Объект подготавливается к использованию.
        /// </summary>
        void Restore();

        IObjectPool Owner { get; }
    }

    /// <summary>
    /// Тривиальная реализация ICollectable с проверками.
    /// </summary>
    public abstract class Collectable : ICollectable, ISingleRef, System.IDisposable
    {
        private enum State
        {
            Constructed = 0,
            Initialized,
            Collected,
        }

        private IObjectPool mOwner;
        private State mUseState = State.Constructed;

        public bool IsCollected
        {
            get
            {
                return mUseState == State.Collected;
            }
        }

        protected abstract void Collect();
        protected virtual void Restore() { }

        void ICollectable.Initialize(IObjectPool owner)
        {
            mUseState = State.Initialized;
            mOwner = owner;
        }

        public virtual void MakeFakeCollected()
        {
            mUseState = State.Collected;
        }

        void ICollectable.Collect()
        {
            if (mUseState != State.Initialized)
            {
                DBG.Diagnostics.Assert(false, "ObjectPool Warning: object of type {0} collected twice or was not inialized at all", GetType());
            }
            mUseState = State.Collected;
            Collect();
        }

        void ICollectable.Restore()
        {
            if (mUseState != State.Collected)
            {
                DBG.Diagnostics.Assert(false, "ObjectPool Warning: object of type {0} was not properly collected", GetType());
            }
            mUseState = State.Initialized;
            Restore();
        }

        IObjectPool ICollectable.Owner
        {
            get { return mOwner; }
        }

#if MEM_CHECK

        //System.Diagnostics.StackTrace mST = new System.Diagnostics.StackTrace();

        ~Collectable()
        {
            if (mUseState == State.Initialized)
            {
                DBG.Diagnostics.Assert(false, "Memory Warning: Potential memory leak detected. Instance of type {0} is not collected properly", GetType());
                //DBG.Diagnostics.Assert(false, mST.ToString());
            }
        }
#endif

        public virtual void Release()
        {
            DBG.Diagnostics.Assert(mOwner != null);
            if (mOwner != null)
            {
                mOwner.Free(this);
            }
        }

        public void Dispose()
        {
            Release();
        }

        public static void Free<ReleasableT>(ref ReleasableT obj) where ReleasableT : class, IReleasable
        {
            if (obj != null)
            {
                obj.Release();
                obj = null;
            }
        }

        public static void Set<MultiRefT>(ref MultiRefT to, MultiRefT from) where MultiRefT : class, IMultiRef
        {
            to = from;
            if (to != null)
            {
                to.AddRef();
            }
        }
    }

    public abstract class MultyRefCollectable : Collectable, IMultiRef
    {
        protected int mRefCount = 1;

        public bool IsAlive
        {
            get { return mRefCount > 0; }
        }

        public void AddRef() { mRefCount++; }

        public override void MakeFakeCollected()
        {
            base.MakeFakeCollected();
            mRefCount = 0;
        }

        public override void Release()
        {
            if (mRefCount <= 0)
            {
                Log.e("Too many releases. {0}", GetType());
                return;
            }

            mRefCount--;
            if (mRefCount == 0)
            {
                base.Release();
            }
        }

        protected override void Collect()
        {
            if (mRefCount > 0)
            {
                Log.e("Force release detected, ref count {0}. {1}", mRefCount, GetType());
            }
        }

        protected override void Restore()
        {
            base.Restore();

            mRefCount = 1;
        }

#if MEM_CHECK

        ~MultyRefCollectable()
        {
            if (!IsCollected)
            {
                DBG.Diagnostics.Assert(false, "Memory Warning: destroing with RefCount {0}. {1}", mRefCount, GetType());
            }
        }
#endif
    }

}
