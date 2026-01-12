using System;
using Archivarius;

namespace Pontifex.Api.Protocol
{
    public class C2SMessageDecl<TMessage> : MessageDecl<TMessage>
        where TMessage : IDataStruct, new()
    {
        public C2SMessageDecl(Type[]? typesToRegister = null)
            : base(typesToRegister)
        {
        }
    }
}