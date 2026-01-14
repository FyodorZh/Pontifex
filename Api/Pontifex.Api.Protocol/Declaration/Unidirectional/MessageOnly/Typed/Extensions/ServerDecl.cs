using System;
using Archivarius;

namespace Pontifex.Api.Server
{
    public static class MessageDecl_Ext
    {
        public static void Send<TMessage>(this S2CMessageDecl<TMessage> decl, TMessage message)
            where TMessage : struct, IDataStruct
        {
            ((ISender<TMessage>)decl).Send(message);
        }

        public static void SetProcessor<TMessage>(this C2SMessageDecl<TMessage> decl, Action<TMessage> processor)
            where TMessage : struct, IDataStruct
        {
            ((IReceiver<TMessage>)decl).SetProcessor(processor);
        }
    }
}
