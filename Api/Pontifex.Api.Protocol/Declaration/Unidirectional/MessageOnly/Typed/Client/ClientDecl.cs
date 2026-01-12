using System;
using Archivarius;

namespace Pontifex.Api.Protocol.Client
{
    public static class MessageDecl_Ext
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