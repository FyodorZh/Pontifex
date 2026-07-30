using Actuarius.Memory;
using Scriba;

namespace Pontifex.NoAck.Raw.Unreliable
{
    public abstract class NoAckRawUnreliableClientTransport : NoAckRawUnreliableTransport
    {
        protected new INoAckRawUnreliableClientConformanceControl Conformance => (INoAckRawUnreliableClientConformanceControl)base.Conformance;
     
        protected NoAckRawUnreliableClientTransport(string typeName, ILogger logger, IMemoryRental memory,
            NoAckRawUnreliableClientConformanceControl? conformanceControl = null) 
            : base(typeName, logger, memory, conformanceControl ?? new NoAckRawUnreliableClientConformanceControl())
        {
        }

        protected abstract bool TryMakeReliableForDebug();
        
        protected class NoAckRawUnreliableClientConformanceControl : NoAckRawUnreliableConformanceControl, INoAckRawUnreliableClientConformanceControl
        {
            public bool TryMakeReliable()
            {
                var owner = (NoAckRawUnreliableClientTransport)_owner;
                return owner.TryMakeReliableForDebug();
            }
        }
    }
}