using System;
using System.Collections.Generic;
using Archivarius;
using Pontifex.Utils;

namespace Pontifex.Api.Protocol
{
    public abstract class RawMessageDecl : Declaration, IRawSender, IRawReceiver
    {
        private readonly Type[]? _typesToRegister;
        private Action<UnionDataList>? _processor;

        private bool _stopped;

        protected RawMessageDecl(Type[]? typesToRegister = null)
        {
            _typesToRegister = typesToRegister;
        }

        protected override void FillFactoryModels(HashSet<Type> types)
        {
            if (_typesToRegister != null)
            {
                foreach (var type in _typesToRegister)
                {
                    types.Add(type);
                }
            }
        }

        public override void Stop()
        {
            _stopped = true;
        }

        protected sealed override void FillNonFactoryModels(HashSet<Type> types)
        {
        }

        protected sealed override bool OnReceived(ISerializer received)
        {
            throw new InvalidOperationException("RawMessageDecl type doesn't support typed data");
        }

        protected override bool OnReceived(UnionDataList buffer)
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

            return false;
        }

        SendResult IRawSender.Send(UnionDataList message)
        {
            return Send(message);
        }

        void IRawReceiver.SetProcessor(Action<UnionDataList> processor)
        {
            _processor = processor;
        }
    }
}