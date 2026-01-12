using System;
using System.Collections.Generic;
using Archivarius;
using Pontifex.Utils;

namespace Pontifex.Api.Protocol
{
    public abstract class MessageDecl<TMessage> : Declaration, ISender<TMessage>, IReceiver<TMessage>
        where TMessage : IDataStruct, new()
    {
        private readonly Type[]? mTypesToRegister;
        private Action<TMessage>? mProcessor;

        private bool mStopped;

        protected MessageDecl(Type[]? typesToRegister = null)
        {
            mTypesToRegister = typesToRegister;
        }

        protected override void FillFactoryModels(HashSet<Type> types)
        {
            if (mTypesToRegister != null)
            {
                foreach (var type in mTypesToRegister)
                {
                    types.Add(type);
                }
            }
        }

        public override void Stop()
        {
            mStopped = true;
        }

        protected sealed override void FillNonFactoryModels(HashSet<Type> types)
        {
            types.Add(typeof(TMessage));
        }

        protected sealed override bool OnReceived(ISerializer received)
        {
            if (!mStopped)
            {
                var processor = mProcessor;
                if (processor != null)
                {
                    TMessage msg = new TMessage();
                    msg.Serialize(received);

                    processor.Invoke(msg);
                    return true;
                }
            }

            return false;
        }

        protected override bool OnReceived(UnionDataList buffer)
        {
            throw new InvalidOperationException("MessageDecl type doesn't support raw data");
        }

        void ISender<TMessage>.Send(TMessage message)
        {
            Send(message);
        }

        void IReceiver<TMessage>.SetProcessor(Action<TMessage> processor)
        {
            mProcessor = processor;
        }
    }
}