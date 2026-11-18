using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack
{
    public abstract class RawReliableAckServer : AnyTransport, IRawReliableAckServer
    {
        private bool _isInitialized;

        private IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>? _acknowledger;
        
        public override TransportType Type => TransportType.RawReliableAck;

        protected RawReliableAckServer(string typeName, ILogger logger, IMemoryRental memory)
            :base(typeName, logger, memory)
        {
        }

        protected virtual IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>? SetupAcknowledger(IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> acknowledger)
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

        public bool Init(IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> acknowledger)
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

        /// <summary>
        /// Asks the acknowledger to validate a client's acknowledgement data, create its session handler,
        /// and build the ACK response for the client.
        /// </summary>
        /// <param name="remote">The remote endpoint of the connecting client.</param>
        /// <param name="ackData">The acknowledgement request. This method adds a reference and passes it to
        /// the acknowledger; the caller retains its own reference and this method releases that own
        /// reference before returning.</param>
        /// <returns>The session handler wrapped with the debugging and safety wrappers, together with the
        /// ACK response built by the acknowledger; or <see langword="null"/> when the client is rejected or
        /// the acknowledger returned an unusable result. The caller owns the returned response and must
        /// release it exactly once unless it hands ownership to a session endpoint.</returns>
        protected (IRawReliableAckServerHandler handler, UnionDataList ackResponse)? TryConnectNewClient(
            IEndPoint remote, UnionDataList ackData)
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
            if (acknowledger == null)
            {
                return null;
            }

            var result = acknowledger.TryAck(remote, ackData.Acquire());
            if (result == null)
            {
                return null;
            }

            var handler = result.Value.handler;
            var ackResponse = result.Value.ackResponse;

            if (handler == null)
            {
                ackResponse?.Release();
                return null;
            }

            if (ackResponse == null)
            {
                try
                {
                    handler.OnStopped(new TextFail(Name, "Acknowledger returned no ACK response"));
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }

                return null;
            }

            handler = handler.Test(text => Log.e(text)).GetSafe(e => Log.e(e.ToString()));
            return (handler, ackResponse);
        }

    }
}
