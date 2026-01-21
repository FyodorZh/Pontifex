using System;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public abstract class RawMessageDecl : Declaration, IRawSender, IRawReceiver
    {
        private IUnidirectionalRawPipeIn? _pipeIn;
        private IUnidirectionalRawPipeOut? _pipeOut;
        
        private Action<UnionDataList>? _processor;

        private bool _stopped;

        protected void SetPipeIn(IUnidirectionalRawPipeIn pipeIn)
        {
            _pipeIn = pipeIn;
        }
        
        protected void SetPipeOut(IUnidirectionalRawPipeOut pipeOut)
        {
            _pipeOut = pipeOut;
            pipeOut.SetReceiver(OnReceived);
        }
        
        public override void Stop()
        {
            _stopped = true;
            _pipeIn = null;
            _pipeOut?.SetReceiver(null);
            _pipeOut = null;
        }

        private bool OnReceived(UnionDataList buffer)
        {
            if (!_stopped)
            {
                var processor = _processor;
                if (processor != null)
                {
                    processor.Invoke(buffer);
                    return true;
                }
            }

            buffer.Release();
            return false;
        }

        SendResult IRawSender.Send(UnionDataList message)
        {
            return _pipeIn?.Send(message) ?? SendResult.NotConnected;
        }

        void IRawReceiver.SetProcessor(Action<UnionDataList> processor)
        {
            _processor = processor;
        }
    }
}