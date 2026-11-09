using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack
{
    public abstract class RawReliableAckClientTransport : RawReliableClientTransport<IRawReliableAckClientHandler>, IRawReliableAckClient
    {
        public override TransportType Type => TransportType.RawReliableAck;
        
        protected RawReliableAckClientTransport(string typeName, ILogger logger, IMemoryRental memory) 
            : base(typeName, logger, memory, new RawReliableConformanceControl())
        {
        }
        
        protected void ConnectionFinished(RawReliableAckClientEndpoint endPoint, UnionDataList ackResponse)
        {
            using var ackResponseDisposer = ackResponse.AsDisposable();
            ConnectionFinished((handler) =>
            {
                try
                {
                    handler.OnConnected(endPoint, ackResponse.Acquire());
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    Stop(new ExceptionFail(Name, ex, "OnConnected failed"));
                }
            });
        }
    }
}