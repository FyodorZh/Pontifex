using System;
using Serializer.BinarySerializer;
using Serializer.Factory;

namespace Serializer.Extensions.Pool
{
    public class TypedModelConstructorWithPool<T> : IModelConstructor
        where T : class, IDataStruct, Shared.Pool.ICollectable, new()        
    {
        private static readonly Type mType = typeof(T);

        Type IModelConstructor.FromType
        {
            get { return mType; }
        }

        Type IModelConstructor.ToType
        {
            get { return mType; }
        }

        object IModelConstructor.Construct()
        {
            return Shared.Pool.ObjectPool<T>.Allocate();
        }

        object IModelConstructor.Construct(object src)
        {
            return src;
        }
    }
}
