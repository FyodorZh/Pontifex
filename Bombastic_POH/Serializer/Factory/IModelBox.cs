using Serializer.BinarySerializer;

namespace Serializer.Factory
{
    public interface IModelBox<TFrom, TSerializer> : IDataStruct, ITypeProvider
        where TFrom : class, IDataStruct, new()
    {
        void Init(TFrom data);
    }
}
