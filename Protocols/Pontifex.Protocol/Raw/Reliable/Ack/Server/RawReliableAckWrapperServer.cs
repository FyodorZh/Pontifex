using System;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Protocols
{
    public class RawReliableAckWrapperServer<TAcknowledgerWrapper> : RawReliableAckServer
        where TAcknowledgerWrapper : AcknowledgerWrapper
    {
        private readonly IRawReliableAckServer _core;

        private readonly Func<ILogger, IMemoryRental, TAcknowledgerWrapper> mWrapperConstructor;

        public RawReliableAckWrapperServer(string typeName, IRawReliableAckServer core, Func<ILogger, IMemoryRental, TAcknowledgerWrapper> wrapperConstructor)
            : base(typeName, core.Log, core.Memory)
        {
            _core = core;
            mWrapperConstructor = wrapperConstructor;
        }

        public override int MessageMaxByteSize => _core.MessageMaxByteSize;

        protected override bool TryStart()
        {
            return _core.Start(r =>
            {
                if (IsStarted)
                {
                    Fail(r, "Unexpected underlying transport stop");
                }
            });
        }

        protected override void OnStopped(StopReason reason)
        {
            _core.Stop(reason);
        }

        protected override IRawServerAcknowledger<IRawReliableAckServerHandler>? SetupAcknowledger(IRawServerAcknowledger<IRawReliableAckServerHandler> baseAcknowledger)
        {
            var acknowledger = mWrapperConstructor.Invoke(Log, Memory);
            acknowledger.Init(baseAcknowledger, text => Log.e(text));
            if (_core.Init(acknowledger))
            {
                return acknowledger;
            }
            Fail("SetupAcknowledger", "Failed to init nested transport");
            return null;
        }

        public override string ToString()
        {
            string coreName = _core.ToString();
            return $"{Name}<{coreName}>";
        }
    }
}
