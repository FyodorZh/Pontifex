using System;
using Operarius;

namespace Pontifex.UserApi
{
    public interface IGenericProtocol<out TProtocolDeclarations> : IPeriodicLogic
        where TProtocolDeclarations : Protocol
    {
        event Action Stopped;

        event Action Connected;

        TProtocolDeclarations Protocol { get; }

        void Stop(StopReason? reason = null);
    }
}
