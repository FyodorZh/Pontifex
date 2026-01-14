using Archivarius;

namespace Pontifex.Api
{
    public interface IPipeAllocator
    {
        IUnidirectionalRawPipeIn AllocateRawPipeIn();
        IUnidirectionalRawPipeOut AllocateRawPipeOut();
        IUnidirectionalModelPipeIn<TModel> AllocateModelPipeIn<TModel>() where TModel : struct, IDataStruct;
        IUnidirectionalModelPipeOut<TModel> AllocateModelPipeOut<TModel>() where TModel : struct, IDataStruct;
    }
}