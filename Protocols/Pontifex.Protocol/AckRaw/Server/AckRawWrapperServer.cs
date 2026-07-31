using System;
using Actuarius.Memory;
using Pontifex.Ack.Raw;
using Pontifex.Ack.Raw.Reliable;
using Scriba;

namespace Pontifex.Protocols
{
    public class AckRawWrapperServer<TAcknowledgerWrapper> : AckRawReliableServer
        where TAcknowledgerWrapper : AcknowledgerWrapper
    {
        private readonly IAckRawReliableServer _core;

        private readonly Func<ILogger, IMemoryRental, TAcknowledgerWrapper> mWrapperConstructor;

        public AckRawWrapperServer(string typeName, IAckRawReliableServer core, Func<ILogger, IMemoryRental, TAcknowledgerWrapper> wrapperConstructor)
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

        protected override IRawServerAcknowledger<IAckRawReliableServerHandler>? SetupAcknowledger(IRawServerAcknowledger<IAckRawReliableServerHandler> baseAcknowledger)
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
