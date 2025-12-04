using System;
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

        private readonly Func<TAcknowledgerWrapper> mWrapperConstructor;

        public AckRawWrapperServer(string typeName, IAckRawServer core, Func<TAcknowledgerWrapper> wrapperConstructor)
            : base(typeName)
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
            }, Log);
        }

        protected override void OnStopped(StopReason reason)
        {
            _core.Stop(reason);
        }

        protected override IRawServerAcknowledger<IAckRawServerHandler>? SetupAcknowledger(IRawServerAcknowledger<IAckRawServerHandler> baseAcknowledger)
        {
            var acknowledger = mWrapperConstructor.Invoke();
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
