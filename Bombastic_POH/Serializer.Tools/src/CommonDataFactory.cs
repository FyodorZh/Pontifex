using System;
using System.Collections.Generic;
using Serializer.BinarySerializer;
using Serializer.Factory;

namespace Serializer.Tools
{
    public interface IDataConstructor
    {
        Type DataType { get; }
        object Construct();
    }

    public class TypedDataConstructor<T> : IDataConstructor
        where T : class, IDataStruct, new()
    {
        private static readonly Type mType = typeof(T);

        Type IDataConstructor.DataType
        {
            get { return mType; }
        }

        object IDataConstructor.Construct()
        {
            return new T();
        }
    }

    public abstract class CommonDataFactory : IDataStructFactory
    {
        private readonly Dictionary<short, IDataConstructor> _typesTable = new Dictionary<short, IDataConstructor>();
        private readonly Dictionary<Type, short> _indexTable = new Dictionary<Type, short>();

        private static readonly ushort MAX_ID = (1 << 15);

        protected void Append(ushort id, IDataConstructor constructor)
        {
            if (id >= MAX_ID)
            {
                Log.e("Data model Id {0} must be smoller than {1}", id, MAX_ID);
            }
            IDataConstructor iConst;
            if (_typesTable.TryGetValue((short)id, out iConst))
            {
                if (constructor.DataType != iConst.DataType)
                {
                    Log.e("Resource model id {0} duplicates {1} -> {2}", id, constructor.DataType.Name, iConst.DataType.Name);
                }
                return;
            }
            _typesTable.Add((short)id, constructor);
            _indexTable.Add(constructor.DataType, (short)id);
        }

        bool IDataStructFactory.SerializeModelIndex() { return true; }

        short IDataStructFactory.GetIndexForModel(Type modelType)
        {
            short result;
            if (!_indexTable.TryGetValue(modelType, out result))
            {
                result = -1;
            }
            return result;
        }

        object IDataStructFactory.CreateDataStruct(short modelIndex)
        {
            IDataConstructor ctr;
            if (_typesTable.TryGetValue(modelIndex, out ctr))
            {
                return ctr.Construct();
            }
            return null;
        }
    }
}
