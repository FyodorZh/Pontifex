using System;
using System.Collections.Generic;

namespace Serializer.Factory
{
    public class ModelWrapperFactory : IDataStructFactory
    {
        private readonly List<IModelConstructor> mIndex = new List<IModelConstructor>();
        private readonly Dictionary<Type, int> mTable = new Dictionary<Type, int>();

        public void Clear()
        {
            mIndex.Clear();
            mTable.Clear();
        }

        public void Append(IModelConstructor constructor)
        {
            int id;
            if (!mTable.TryGetValue(constructor.FromType, out id))
            {
                id = mIndex.Count;
                mIndex.Add(constructor);
                mTable[constructor.FromType] = id;
            }
            else
            {
                Log.e("Duplicate model registration for type: " + constructor.FromType);
            }
        }

        public bool SerializeModelIndex()
        {
            return true;
        }

        public short GetIndexForModel(Type modelType)
        {
            int result;
            if (!mTable.TryGetValue(modelType, out result))
            {
                result = -1;
            }
            return (short)result;
        }

        public object CreateDataStruct(short modelIndex)
        {
            IModelConstructor pool = mIndex[modelIndex];
            if (pool != null)
            {
                return pool.Construct();
            }
            return null;
        }
    }
}