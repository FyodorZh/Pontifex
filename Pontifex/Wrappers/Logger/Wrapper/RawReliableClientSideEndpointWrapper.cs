using System;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Logger
{
    public class RawReliableClientSideEndpointWrapper : RawReliableBaseEndpointWrapper, IRawReliableEndpoint
    {
        public RawReliableClientSideEndpointWrapper(IRawReliableEndpoint? core, 
            Func<IRawReliableEndpoint?, UnionDataList, SendResult> sender, 
            Func<IRawReliableEndpoint?, StopReason, bool> disconnector)
            : base(core, sender, disconnector)
        {
        }
    }
}
