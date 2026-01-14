using System;
using System.Collections.Generic;
using Pontifex.Utils;

namespace Pontifex.Api.Server
{
    public static class RawMessageDecl_Ext
    {
        public static SendResult Send(this S2CRawMessageDecl decl, UnionDataList message)
        {
            return ((IRawSender)decl).Send(message);
        }

        public static void Register(this C2SRawMessageDecl decl, Action<UnionDataList> processor)
        {
            ((IRawReceiver)decl).SetProcessor(processor);
        }
    }
}
