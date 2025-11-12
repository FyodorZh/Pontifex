using System;
using System.Collections.Generic;

namespace Serializer.Factory
{
    public sealed class ModelFactory : IDataStructFactory
    {
        private static readonly IModelConstructor[] _modelsTable = new IModelConstructor[8 * 1024];
        private static readonly Dictionary<Type, int> _indexTable = new Dictionary<Type, int>();
        private static readonly Dictionary<Type, IModelConstructor> _objectPools = new Dictionary<Type, IModelConstructor>();

        public static void Clear()
        {
            for (int i = 0; i < _modelsTable.Length; ++i)
            {
                _modelsTable[i] = null;
            }
            _indexTable.Clear();
            _objectPools.Clear();
        }

        public static void Append(ushort id, IModelConstructor constructor)
        {
            if (id >= _modelsTable.Length)
            {
                Log.e("Protocol model id {0} is too big", id);
                return;
            }

            if (_modelsTable[id] != null)
            {
                IModelConstructor iConst = _modelsTable[id];
                if (constructor.FromType != iConst.FromType || constructor.ToType != iConst.ToType)
                {
                    Log.e("Protocol model id {0} dublucates [{1}, {2}] -> [{3}, {4}]", id, constructor.FromType.Name, constructor.ToType.Name, iConst.FromType.Name, iConst.ToType.Name);
                }
                return;
            }
            _modelsTable[id] = constructor;
            _indexTable.Add(constructor.FromType, id);
            _objectPools.Add(constructor.ToType, constructor);
        }

        public static void Extend(IModelConstructor constructor)
        {
            int id;
            if (_indexTable.TryGetValue(constructor.FromType, out id))
            {
                _modelsTable[id] = constructor;
                _objectPools[constructor.FromType] = constructor;
            }
        }

        public static IModelConstructor FindConstructor(Type fromType)
        {
            IModelConstructor ctor;
            _objectPools.TryGetValue(fromType, out ctor);
            return ctor;
        }

        public bool SerializeModelIndex()
        {
            return true;
        }

        public short GetIndexForModel(Type modelType)
        {
            int result;
            if (!_indexTable.TryGetValue(modelType, out result))
            {
                result = -1;
            }
            return (short)result;
        }

        public object CreateDataStruct(short modelIndex)
        {
            IModelConstructor pool = _modelsTable[modelIndex];
            if (pool != null)
            {
                return pool.Construct();
            }
            return null;
        }
    }
}
