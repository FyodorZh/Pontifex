using System;
using Archivarius;

namespace Pontifex.Api
{
    public interface IUnidirectionalModelPipeIn<in TModel> : ITransportPipe
        where TModel : struct, IDataStruct
    {
        SendResult Send(TModel model);
    }
    
    public interface IUnidirectionalModelPipeOut<out TModel> : ITransportPipe
        where TModel : struct, IDataStruct
    {
        void SetReceiver(Func<TModel, bool>? receiver);
    }
}