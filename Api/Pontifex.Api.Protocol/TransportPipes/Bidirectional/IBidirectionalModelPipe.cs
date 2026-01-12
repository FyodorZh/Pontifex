using Archivarius;

namespace Pontifex.Api.Protocol
{
    public interface IBidirectionalModelPipe<TModel> : IUnidirectionalModelPipeIn<TModel>, IUnidirectionalModelPipeOut<TModel>
        where TModel : class, IDataStruct
    {
    }
}