using System;
using Operarius;

namespace Pontifex.Raw.Reliable.Ack
{
    public abstract class RawReliableAckServerEndpoint : RawReliableServerEndpoint
    {
        public RawReliableAckServerEndpoint(IRawReliableTransport owner, IEndPoint remoteEndPoint, 
            IRawReliableServerHandler handler, 
            int messageMaxByteSize, long bufferCapacityBytes, 
            TimeSpan disconnectTimeout, ILogicRunner<IPeriodicLogicDriverCtl> runner) 
            : base(owner, remoteEndPoint, handler, 
                messageMaxByteSize, bufferCapacityBytes, 
                disconnectTimeout, runner)
        {
        }
    }
}