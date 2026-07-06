using System;
using Pontifex.Utils;

namespace Pontifex.Ack.Raw
{
    public class AckRawClientSideEndpointWrapper : AckRawBaseEndpointWrapper, IAckRawReliableClientSideEndpoint
    {
        public AckRawClientSideEndpointWrapper(IAckRawReliableClientSideEndpoint? core, 
            Func<IAckRawReliableBaseEndpoint?, UnionDataList, SendResult> sender, 
            Func<IAckRawReliableBaseEndpoint?, StopReason, bool> disconnector)
            : base(core, sender, disconnector)
        {
        }
    }
}