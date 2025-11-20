using Actuarius.Collections;
using Actuarius.Memory;

namespace Shared.Pooling
{
    public abstract class ConcurrentPool<TObject, TParam1> : Pool<TObject, TParam1>, IConcurrentPool<TObject, TParam1>
        where TObject : class
    {
        private class ConcurrentConstructorObscure: IConstructor<IPool<TObject>, int>
        {
            private readonly IConstructor<IConcurrentPool<TObject>, int> mCtor;

            public ConcurrentConstructorObscure(IConstructor<IConcurrentPool<TObject>, int> ctor)
            {
                mCtor = ctor;
            }

            public IPool<TObject> Construct(int param1)
            {
                return mCtor.Construct(param1);
            }
        }

        protected ConcurrentPool(IConstructor<IConcurrentPool<TObject>, int> ctor, IConcurrentMap<int, IPool<TObject>> table)
            : base(new ConcurrentConstructorObscure(ctor), table)
        {
        }
    }
}