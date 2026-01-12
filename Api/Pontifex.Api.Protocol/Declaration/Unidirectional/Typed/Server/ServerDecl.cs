using System;
using Archivarius;

namespace Pontifex.Api.Protocol.Server
{
    public static class MessageDecl_Ext
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
