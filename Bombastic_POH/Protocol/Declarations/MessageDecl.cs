using System;
using System.Collections.Generic;
using Serializer.BinarySerializer;
using Shared.Buffer;

namespace NewProtocol
{
    internal interface ISender<in TMessage>
    {
        void Send(TMessage message);
    }

    internal interface IReceiver<TMessage>
    {
        void SetProcessor(Action<TMessage> processor);
    }

    public abstract class MessageDecl<TMessage> : Declaration, ISender<TMessage>, IReceiver<TMessage>
        where TMessage : IDataStruct, new()
    {
        private readonly Type[] mTypesToRegister;
        private Action<TMessage> mProcessor;

        private bool mStopped;

        protected MessageDecl(Type[] typesToRegister = null)
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

        protected sealed override bool OnReceived(IBinarySerializer received)
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

        protected override bool OnReceived(IMemoryBufferHolder buffer)
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

    public class S2CMessageDecl<TMessage> : MessageDecl<TMessage>
        where TMessage : IDataStruct, new()
    {
        public S2CMessageDecl(Type[] typesToRegister = null)
            : base(typesToRegister)
        {

        }
    }

    public class C2SMessageDecl<TMessage> : MessageDecl<TMessage>
        where TMessage : IDataStruct, new()
    {
        public C2SMessageDecl(Type[] typesToRegister = null)
            : base(typesToRegister)
        {

        }
    }

    namespace Client
    {
        public static class MessageDecl
        {
            public static void Send<TMessage>(this C2SMessageDecl<TMessage> decl, TMessage message)
                where TMessage : IDataStruct, new()
            {
                ((ISender<TMessage>)decl).Send(message);
            }

            public static void Register<TMessage>(this S2CMessageDecl<TMessage> decl, Action<TMessage> processor)
                where TMessage : IDataStruct, new()
            {
                ((IReceiver<TMessage>)decl).SetProcessor(processor);
            }
        }
    }

    namespace Server
    {
        public static class MessageDecl
        {
            public static void Send<TMessage>(this S2CMessageDecl<TMessage> decl, TMessage message)
                where TMessage : IDataStruct, new()
            {
                ((ISender<TMessage>)decl).Send(message);
            }

            public static void Register<TMessage>(this C2SMessageDecl<TMessage> decl, Action<TMessage> processor)
                where TMessage : IDataStruct, new()
            {
                ((IReceiver<TMessage>)decl).SetProcessor(processor);
            }
        }
    }
}
