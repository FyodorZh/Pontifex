using System;
using Archivarius;

namespace Pontifex.Api.Protocol
{
    public interface IUnidirectionalModelPipeIn<in TModel> : ITransportPipe
        where TModel : class, IDataStruct
    {
        SendResult Send(TModel model);
    }
    
    public interface IUnidirectionalModelPipeOut<out TModel> : ITransportPipe
        where TModel : class, IDataStruct
    {
        void SetReceiver(Func<TModel, bool> receiver);
    }
}