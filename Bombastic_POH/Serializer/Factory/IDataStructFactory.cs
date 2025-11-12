using System;

namespace Serializer.Factory
{
    public interface IDataStructFactory
    {
        bool SerializeModelIndex(); // убейте меня, кто сможет
        short GetIndexForModel(Type modelType);
        object CreateDataStruct(short modelIndex);
    }

    public interface IConcurrentDataStructFactory : IDataStructFactory
    {
    }
}
