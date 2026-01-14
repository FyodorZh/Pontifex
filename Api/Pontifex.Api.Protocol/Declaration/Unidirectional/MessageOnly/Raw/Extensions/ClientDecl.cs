using System;
using Pontifex.Utils;

namespace Pontifex.Api.Protocol.Client
{
    public static class RawMessageDecl_Ext
    {
        public static SendResult Send(this C2SRawMessageDecl decl, UnionDataList message)
        {
            return ((IRawSender)decl).Send(message);
        }

        public static void Register(this S2CRawMessageDecl decl, Action<UnionDataList> processor)
        {
            ((IRawReceiver)decl).SetProcessor(processor);
        }
    }
}