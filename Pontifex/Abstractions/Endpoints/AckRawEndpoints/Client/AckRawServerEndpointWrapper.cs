using System;
using Pontifex.Utils;

namespace Pontifex.Ack.Raw
{
    public class AckRawClientSideEndpointWrapper : AckRawBaseEndpointWrapper, IAckRawClientSideEndpoint
    {
        public AckRawClientSideEndpointWrapper(IAckRawClientSideEndpoint? core, 
            Func<IAckRawBaseEndpoint?, UnionDataList, SendResult> sender, 
            Func<IAckRawBaseEndpoint?, StopReason, bool> disconnector)
            : base(core, sender, disconnector)
        {
        }
    }
}