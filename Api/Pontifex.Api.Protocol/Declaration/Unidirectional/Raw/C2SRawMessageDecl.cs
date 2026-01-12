using System;

namespace Pontifex.Api.Protocol
{
    public class C2SRawMessageDecl : RawMessageDecl
    {
        public C2SRawMessageDecl(Type[]? typesToRegister = null)
            : base(typesToRegister)
        {
        }
    }
}