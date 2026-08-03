using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack
{
    public abstract class RawReliableAckServer : AnyTransport, IRawReliableAckServer
    {
        private bool _isInitialized;

        private IRawServerAcknowledger<IRawReliableAckServerHandler>? _acknowledger;
        
        public override TransportType Type => TransportType.RawReliableAck;

        protected RawReliableAckServer(string typeName, ILogger logger, IMemoryRental memory)
            :base(typeName, logger, memory)
        {
        }

        protected virtual IRawServerAcknowledger<IRawReliableAckServerHandler>? SetupAcknowledger(IRawServerAcknowledger<IRawReliableAckServerHandler> acknowledger)
        {
            return acknowledger;
        }

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

        public bool Init(IRawServerAcknowledger<IRawReliableAckServerHandler> acknowledger)
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

        protected IRawReliableAckServerHandler? TryConnectNewClient(UnionDataList ackData)
        {
            using var ackDataDisposer = ackData.AsDisposable();
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
                var handler = acknowledger.TryAck(ackData.Acquire());
                handler = handler?.Test(text => Log.e(text)).GetSafe(e => Log.e(e.ToString()));
                return handler;
            }
            return null;
        }

    }
}
