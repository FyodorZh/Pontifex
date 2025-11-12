using System;
using Serializer.BinarySerializer;
using Shared.Pool;

namespace Serializer.Factory
{
    public class ModelWrapperWithPool<TFrom, TTo, TSerializer> : IModelConstructor
        where TFrom : class, IDataStruct, ICollectable, new()
        where TTo : class, IModelBox<TFrom, TSerializer>, ICollectable, new()
    {
        private static readonly Type mFromType = typeof(TFrom);
        private static readonly Type mToType = typeof(TTo);

        public Type FromType
        {
            get { return mFromType; }
        }

        public Type ToType
        {
            get { return mToType; }
        }

        public object Construct()
        {
            TTo ttt = ObjectPool<TTo>.Allocate();
            ttt.Init(ObjectPool<TFrom>.Allocate());
            return ttt;
        }

        public object Construct(object src)
        {
            TTo ttt = ObjectPool<TTo>.Allocate();
            ttt.Init((TFrom)src);
            return ttt;
        }
    }
}
