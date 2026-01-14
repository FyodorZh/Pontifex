using System;
using Archivarius;

namespace Pontifex.Api.Protocol
{
    public abstract class MessageDecl<TMessage> : Declaration, ISender<TMessage>, IReceiver<TMessage>
        where TMessage : struct, IDataStruct
    {
        private IUnidirectionalModelPipeIn<TMessage>? _pipeIn;
        private IUnidirectionalModelPipeOut<TMessage>? _pipeOut;
        
        private Action<TMessage>? _processor;

        private bool _stopped;
        
        protected void SetPipeIn(IUnidirectionalModelPipeIn<TMessage> pipeIn)
        {
            _pipeIn = pipeIn;
        }
        
        protected void SetPipeOut(IUnidirectionalModelPipeOut<TMessage> pipeOut)
        {
            _pipeOut = pipeOut;
            pipeOut.SetReceiver(OnReceived);
        }

        public override void Stop()
        {
            _stopped = true;
        }

        private bool OnReceived(TMessage received)
        {
            if (!_stopped)
            {
                var processor = _processor;
                if (processor != null)
                {
                    processor.Invoke(received);
                    return true;
                }
            }

            return false;
        }

        SendResult ISender<TMessage>.Send(TMessage message)
        {
            return _pipeIn?.Send(message) ?? SendResult.NotConnected;
        }

        void IReceiver<TMessage>.SetProcessor(Action<TMessage> processor)
        {
            _processor = processor;
        }
    }
}