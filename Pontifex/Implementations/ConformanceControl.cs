using System;
using System.Threading;
using Pontifex.StopReasons;
using Pontifex.Utils.CheckPointGate;

namespace Pontifex
{
    public class ConformanceControl : IConformanceControl
    {
        protected AnyTransport _owner = null!;

        private readonly CheckPoint _beforeStopStateTransitionGate = new();
        private readonly CheckPoint _beforeStoppedCallbackGate = new();

        private int _failNextStartFlag;

        public virtual string Name => "ConformanceControl(AnyTransport)";

        public ICheckPointCtl BeforeStopStateTransitionGate => _beforeStopStateTransitionGate;

        public ICheckPointCtl BeforeStoppedCallbackGate => _beforeStoppedCallbackGate;

        protected virtual void OnOwnerSet() { }

        public void SetOwner(AnyTransport owner)
        {
            _owner = owner;
            OnOwnerSet();
        }

        public virtual void FailNextStart()
        {
            if (_owner.HasStartBeenAttempted || Interlocked.Exchange(ref _failNextStartFlag, 1) == 1)
            {
                throw new InvalidOperationException();
            }
        }

        public void InjectUnrecoverableFailure()
        {
            if (!_owner.IsStarted || !_owner.IsValid)
            {
                throw new InvalidOperationException();
            }
            _owner.Fail(new TextFail(Name, "ConformanceControl(AnyTransport) injected unrecoverable failure"));
        }

        public bool ShouldFailNextStart_AnyTransportLevel()
        {
            return Volatile.Read(ref _failNextStartFlag) != 0;
        }
    }
}
