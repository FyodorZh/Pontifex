namespace Shared.Pooling
{
    public abstract class Pool<TObject, TParam1> : IPool<TObject, TParam1>
        where TObject : class
    {
        private readonly IConstructor<IPool<TObject>, int> mConstructor;
        private readonly IMap<int, IPool<TObject>> mTable;

        protected Pool(IConstructor<IPool<TObject>, int> ctor, IMap<int, IPool<TObject>> table)
        {
            mConstructor = ctor;
            mTable = table;
        }

        protected abstract int Classify(TParam1 param);
        protected abstract int Classify(TObject obj);

        public void Release(TObject obj)
        {
            if (obj != null)
            {
                int id = Classify(obj);
                IPool<TObject> subPool;
                if (mTable.TryGetValue(id, out subPool))
                {
                    subPool.Release(obj);
                }
                else
                {
                    Log.e("Error!!!");
                }
            }
        }

        public TObject Acquire(TParam1 param1)
        {
            int id = Classify(param1);

            IPool<TObject> subPool;
            if (!mTable.TryGetValue(id, out subPool))
            {
                subPool = mConstructor.Construct(id);
                mTable.Add(id, subPool);
            }

            return subPool.Acquire();
        }
    }
}