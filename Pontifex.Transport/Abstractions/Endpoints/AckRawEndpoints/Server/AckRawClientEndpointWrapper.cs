using System;
using Pontifex.Utils;

namespace Transport.Abstractions.Endpoints.Server
{
    public class AckRawClientEndpointWrapper : AckRawBaseEndpointWrapper, IAckRawClientEndpoint
    {
        public AckRawClientEndpointWrapper(IAckRawClientEndpoint? core,  
            Func<IAckRawBaseEndpoint?, UnionDataList, SendResult> sender, 
            Func<IAckRawBaseEndpoint?, StopReason, bool> disconnector)
            : base(core, sender, disconnector)
        {
        }
    }
}