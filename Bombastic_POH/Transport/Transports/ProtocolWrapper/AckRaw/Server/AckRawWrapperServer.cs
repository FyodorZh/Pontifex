using Shared;
using Transport.Abstractions.Acknowledgers;
using Transport.Abstractions.Handlers.Server;
using Transport.Abstractions.Servers;
using Transport.Transports.Core;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public class AckRawWrapperServer<TAcknowledgerWrapper> : AckRawServer
        where TAcknowledgerWrapper : AcknowledgerWrapper
    {
        private readonly IAckRawServer mCore;

        private readonly IConstructor<TAcknowledgerWrapper> mWrapperConstructor;

        public AckRawWrapperServer(string typeName, IAckRawServer core, IConstructor<TAcknowledgerWrapper> wrapperConstructor)
            : base(typeName)
        {
            mCore = core;
            mWrapperConstructor = wrapperConstructor;
            AppendControl(core);
        }

        public override int MessageMaxByteSize
        {
            get
            {
                return mCore.MessageMaxByteSize;
            }
        }

        protected override bool TryStart()
        {
            return mCore.Start(r =>
            {
                if (IsStarted)
                {
                    Fail(r, "Unexpected underlying transport stop");
                }
            }, Log);
        }

        protected override void OnStopped(StopReason reason)
        {
            mCore.Stop(reason);
        }

        protected override IRawServerAcknowledger<IAckRawServerHandler> SetupAcknowledger(IRawServerAcknowledger<IAckRawServerHandler> baseAcknowledger)
        {
            var acknowledger = mWrapperConstructor.Construct();
            acknowledger.Init(baseAcknowledger, text => Log.e(text));
            if (mCore.Init(acknowledger))
            {
                return acknowledger;
            }
            Fail("SetupAcknowledger", "Failed to init nested transport");
            return null;
        }

        public override string ToString()
        {
            string coreName = mCore != null ? mCore.ToString() : "null-core";
            return string.Format("{0}<{1}>", Type, coreName);
        }
    }
}
