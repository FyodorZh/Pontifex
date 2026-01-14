using System;

namespace Pontifex.Api
{
    public interface IUnidirectionalModelPipeIn<in TModel> : ITransportPipe
        where TModel : struct
    {
        SendResult Send(TModel model);
    }
    
    public interface IUnidirectionalModelPipeOut<out TModel> : ITransportPipe
        where TModel : struct
    {
        void SetReceiver(Action<TModel>? receiver);
    }
}