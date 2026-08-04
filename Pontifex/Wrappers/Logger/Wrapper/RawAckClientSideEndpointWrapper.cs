using System;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack.Logger
{
    public class RawAckClientSideEndpointWrapper : RawAckBaseEndpointWrapper, IRawReliableEndpoint
    {
        public RawAckClientSideEndpointWrapper(IRawReliableEndpoint? core, 
            Func<IRawReliableEndpoint?, UnionDataList, SendResult> sender, 
            Func<IRawReliableEndpoint?, StopReason, bool> disconnector)
            : base(core, sender, disconnector)
        {
        }
    }
}