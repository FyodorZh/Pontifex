using System;
using Serializer.BinarySerializer;

namespace Serializer.Factory
{
    public class TypedModelConstructor<TDataStruct> : IModelConstructor
        where TDataStruct : class, IDataStruct, new()
    {
        private static readonly Type mType = typeof(TDataStruct);

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
            return new TDataStruct();
        }

        object IModelConstructor.Construct(object src)
        {
            return src;
        }
    }
}
