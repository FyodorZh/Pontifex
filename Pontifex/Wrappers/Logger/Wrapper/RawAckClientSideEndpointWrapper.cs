using System;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack.Logger
{
    public class RawAckClientSideEndpointWrapper : RawAckBaseEndpointWrapper, IRawReliableAckClientSideEndpoint
    {
        public RawAckClientSideEndpointWrapper(IRawReliableAckClientSideEndpoint? core, 
            Func<IRawReliableAckBaseEndpoint?, UnionDataList, SendResult> sender, 
            Func<IRawReliableAckBaseEndpoint?, StopReason, bool> disconnector)
            : base(core, sender, disconnector)
        {
        }
    }
}