using System;
using Serializer.BinarySerializer;

namespace Serializer.Factory
{
    public abstract class ModelBox<TFrom> : IModelBox<TFrom, IBinarySerializer>
        where TFrom : class, IDataStruct, new()
    {
        private static readonly Type mType = typeof(TFrom);

        private TFrom mData;

        public TFrom Data { get { return mData; } }

        protected ModelBox(TFrom data)
        {
            mData = data;
        }

        public virtual void Init(TFrom data)
        {
            mData = data;
        }

        bool IDataStruct.Serialize(IBinarySerializer serializer)
        {
            mData.Serialize(serializer);
            return true;
        }

        Type ITypeProvider.GetType()
        {
            return mType;
        }
    }
}
