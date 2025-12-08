using System;
using Pontifex.Utils;

namespace Transport.Abstractions.Endpoints.Client
{
    public class AckRawServerEndpointWrapper : AckRawBaseEndpointWrapper, IAckRawServerEndpoint
    {
        public AckRawServerEndpointWrapper(IAckRawServerEndpoint? core, 
            Func<IAckRawBaseEndpoint?, UnionDataList, SendResult> sender, 
            Func<IAckRawBaseEndpoint?, StopReason, bool> disconnector)
            : base(core, sender, disconnector)
        {
        }
    }
}