using System;
using Actuarius.Memory;
using Scriba;
using Transport.Abstractions.Acknowledgers;
using Transport.Abstractions.Handlers.Server;
using Transport.Abstractions.Servers;
using Transport.Transports.Core;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public class AckRawWrapperServer<TAcknowledgerWrapper> : AckRawServer
        where TAcknowledgerWrapper : AcknowledgerWrapper
    {
        private readonly IAckRawServer _core;

        private readonly Func<ILogger, IMemoryRental, TAcknowledgerWrapper> mWrapperConstructor;

        public AckRawWrapperServer(string typeName, IAckRawServer core, Func<ILogger, IMemoryRental, TAcknowledgerWrapper> wrapperConstructor)
            : base(typeName, core.Log, core.Memory)
        {
            _core = core;
            mWrapperConstructor = wrapperConstructor;
            AppendControl(core);
        }

        public override int MessageMaxByteSize
        {
            get
            {
                return _core.MessageMaxByteSize;
            }
        }

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

        protected override IRawServerAcknowledger<IAckRawServerHandler>? SetupAcknowledger(IRawServerAcknowledger<IAckRawServerHandler> baseAcknowledger)
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
            return $"{Type}<{coreName}>";
        }
    }
}
