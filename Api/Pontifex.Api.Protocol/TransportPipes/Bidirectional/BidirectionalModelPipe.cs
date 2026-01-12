using System;
using Archivarius;

namespace Pontifex.Api.Protocol
{
    public class BidirectionalModelPipe<TModel> : IBidirectionalModelPipe<TModel>
        where TModel : class, IDataStruct
    {
        private readonly IUnidirectionalModelPipeIn<TModel> _inPipe;
        private readonly IUnidirectionalModelPipeOut<TModel> _outPipe;
        
        public BidirectionalModelPipe(IUnidirectionalModelPipeIn<TModel> inPipe, IUnidirectionalModelPipeOut<TModel> outPipe)
        {
            _inPipe = inPipe;
            _outPipe = outPipe;
        }
        
        SendResult IUnidirectionalModelPipeIn<TModel>.Send(TModel model)
        {
            return _inPipe.Send(model);
        }

        void IUnidirectionalModelPipeOut<TModel>.SetReceiver(Func<TModel, bool> receiver)
        {
            _outPipe.SetReceiver(receiver);
        }
    }
}