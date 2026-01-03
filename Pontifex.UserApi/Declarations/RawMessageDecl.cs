using System;
using System.Collections.Generic;
using Pontifex.Utils;

namespace Pontifex.UserApi
{
    namespace Client
    {
        public static class RawMessageDeclExt
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

    namespace Server
    {
        public static class RawMessageDeclExt
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
}
