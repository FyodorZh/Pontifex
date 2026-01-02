using System;
using Shared.Utils;
using Transport;

namespace NewProtocol
{
    public interface IGenericProtocol<out TProtocolDeclarations> : IPeriodicLogic
        where TProtocolDeclarations : Protocol
    {
        event Action Stopped;

        event Action Connected;

        TProtocolDeclarations Protocol { get; }

        void Stop(StopReason reason = null);
    }
}
