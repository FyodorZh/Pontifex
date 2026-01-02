using System;
using System.Collections.Generic;

namespace Pontifex.UserApi
{
    internal interface IRawSender
    {
        SendResult Send(IMemoryBufferHolder message);
    }

    internal interface IRawReceiver
    {
        void SetProcessor(Action<IMemoryBufferHolder> processor);
    }

    public abstract class RawMessageDecl : Declaration, IRawSender, IRawReceiver
    {
        private readonly Type[] mTypesToRegister;
        private Action<IMemoryBufferHolder> mProcessor;

        private bool mStopped;

        protected RawMessageDecl(Type[] typesToRegister = null)
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
        }

        protected sealed override bool OnReceived(IBinarySerializer received)
        {
            throw new InvalidOperationException("RawMessageDecl type doesn't support typed data");
        }

        protected override bool OnReceived(IMemoryBufferHolder buffer)
        {
            if (!mStopped)
            {
                var processor = mProcessor;
                if (processor != null)
                {
                    processor.Invoke(buffer);
                    return true;
                }
            }

            return false;
        }

        SendResult IRawSender.Send(IMemoryBufferHolder message)
        {
            return Send(message);
        }

        void IRawReceiver.SetProcessor(Action<IMemoryBufferHolder> processor)
        {
            mProcessor = processor;
        }
    }

    public class S2CRawMessageDecl : RawMessageDecl
    {
        public S2CRawMessageDecl(Type[] typesToRegister = null)
            : base(typesToRegister)
        {

        }
    }

    public class C2SRawMessageDecl : RawMessageDecl
    {
        public C2SRawMessageDecl(Type[] typesToRegister = null)
            : base(typesToRegister)
        {

        }
    }

    namespace Client
    {
        public static class RawMessageDeclExt
        {
            public static SendResult Send(this C2SRawMessageDecl decl, IMemoryBufferHolder message)
            {
                return ((IRawSender)decl).Send(message);
            }

            public static void Register(this S2CRawMessageDecl decl, Action<IMemoryBufferHolder> processor)
            {
                ((IRawReceiver)decl).SetProcessor(processor);
            }
        }
    }

    namespace Server
    {
        public static class RawMessageDeclExt
        {
            public static SendResult Send(this S2CRawMessageDecl decl, IMemoryBufferHolder message)
            {
                return ((IRawSender)decl).Send(message);
            }

            public static void Register(this C2SRawMessageDecl decl, Action<IMemoryBufferHolder> processor)
            {
                ((IRawReceiver)decl).SetProcessor(processor);
            }
        }
    }
}
