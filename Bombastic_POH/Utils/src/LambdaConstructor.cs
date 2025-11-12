using System;

namespace Shared
{
    public class LambdaConstructor<T> : IConstructor<T>
    {
        private readonly Func<T> mCtor;

        public LambdaConstructor(Func<T> constructor)
        {
            mCtor = constructor;
        }

        public T Construct()
        {
            return mCtor();
        }
    }
}