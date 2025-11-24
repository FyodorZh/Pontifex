using Pontifex.Utils;
using Transport.Abstractions.Acknowledgers;
using Transport.Abstractions.Handlers.Server;
using Transport.Abstractions.Servers;

namespace Transport.Transports.Core
{
    public abstract class AckRawServer : AbstractTransport, IAckRawServer
    {
        private bool _isInitialized;

        private IRawServerAcknowledger<IAckRawServerHandler>? _acknowledger;

        protected AckRawServer(string typeName)
        {
            Type = typeName;
        }

        protected virtual IRawServerAcknowledger<IAckRawServerHandler>? SetupAcknowledger(IRawServerAcknowledger<IAckRawServerHandler> acknowledger)
        {
            return acknowledger;
        }

        public sealed override string Type { get; }

        public bool IsInitialized
        {
            get
            {
                lock (_locker)
                {
                    return _isInitialized;
                }
            }
        }

        public bool Init(IRawServerAcknowledger<IAckRawServerHandler> acknowledger)
        {
            lock (_locker)
            {
                if (IsValid)
                {
                    if (!IsStarted && !_isInitialized)
                    {
                        var processedAcknowledger = SetupAcknowledger(acknowledger);
                        if (processedAcknowledger != null)
                        {
                            _acknowledger = processedAcknowledger;
                            _isInitialized = true;
                            return true;
                        }
                        Fail("Init", "SetupAcknowledger() returned null");
                    }
                    else
                    {
                        Fail("Init", "Wrong transport state (initialized={0}, started={1})", _isInitialized, IsStarted);
                    }
                }
                return false;
            }
        }

        protected sealed override void OnStarted()
        {
            // DO NOTHING
        }

        public abstract int MessageMaxByteSize { get; }

        protected IAckRawServerHandler? TryConnectNewClient(UnionDataList ackData)
        {
            if (!IsValid)
            {
                Fail("TryConnectNewClient", "Transport is not valid");
                return null;
            }

            if (!IsStarted || !_isInitialized)
            {
                Fail("TryConnectNewClient", "Wrong transport state (initialized={0}, started={1})", _isInitialized, IsStarted);
                return null;
            }

            var acknowledger = _acknowledger;
            if (acknowledger != null)
            {
                var handler = acknowledger.TryAck(ackData, Log);
                if (handler != null)
                {
                    handler = handler.Test(text => Log.e(text)).GetSafe(e => Log.e(e.ToString()));
                }
                return handler;
            }
            return null;
        }

    }
}
