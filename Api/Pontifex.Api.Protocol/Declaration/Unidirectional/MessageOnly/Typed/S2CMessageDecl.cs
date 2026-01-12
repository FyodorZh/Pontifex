using System;
using Archivarius;

namespace Pontifex.Api.Protocol
{
    public class S2CMessageDecl<TMessage> : MessageDecl<TMessage>
        where TMessage : IDataStruct, new()
    {
        public S2CMessageDecl(Type[]? typesToRegister = null)
            : base(typesToRegister)
        {
        }
    }
}