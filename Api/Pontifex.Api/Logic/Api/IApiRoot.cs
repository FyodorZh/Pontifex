using System;

namespace Pontifex.Api
{
    public interface IApiRoot
    {
        public event Action? Disconnecting;
        public event Action<StopReason>? Disconnected;
        void Start(bool isServerMode, IPipeSystem pipeSystem);
        void Stop();
    }
    
    internal interface IApiRootInternal : IApiRoot
    {
        IDeclaration[] Declarations { get; }
    }
}