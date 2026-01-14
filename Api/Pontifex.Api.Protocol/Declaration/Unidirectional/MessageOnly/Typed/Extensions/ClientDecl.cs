using System;
using Archivarius;

namespace Pontifex.Api.Protocol.Client
{
    public static class MessageDecl_Ext
    {
        public static SendResult Send<TMessage>(this C2SMessageDecl<TMessage> decl, TMessage message)
            where TMessage : struct, IDataStruct
        {
            return ((ISender<TMessage>)decl).Send(message);
        }

        public static void SetProcessor<TMessage>(this S2CMessageDecl<TMessage> decl, Action<TMessage> processor)
            where TMessage : struct, IDataStruct
        {
            ((IReceiver<TMessage>)decl).SetProcessor(processor);
        }
    }
}