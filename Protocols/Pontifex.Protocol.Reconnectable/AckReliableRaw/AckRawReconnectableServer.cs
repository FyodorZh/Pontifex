using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Operarius;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Abstractions.Servers;
using Pontifex.Transports.Core;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Protocols.Reconnectable.AckReliableRaw
{
    public class AckRawReconnectableServer : AckRawServer, IAckReliableRawServer, IRawServerAcknowledger<IAckRawServerHandler>
    {
        private readonly IAckReliableRawServer _coreTransport;
        private readonly TimeSpan _disconnectTimeout;

        private readonly SessionMap<ReconnectableServerLogic> _sessionsMap = new (ReconnectableInfo.ServerConnectionsLimit);
        private ILogicDriver<IPeriodicLogicDriverCtl>? _sessionsLogicDriver;

        public AckRawReconnectableServer(IAckReliableRawServer coreTransport, TimeSpan disconnectTimeout, ILogger logger, IMemoryRental memoryRental)
            : base(ReconnectableInfo.TransportName, logger, memoryRental)
        {
            _coreTransport = coreTransport;
            _disconnectTimeout = disconnectTimeout;
        }

        protected override bool TryStart()
        {
            _sessionsLogicDriver = new ThreadBasedPeriodicMultiLogicDriver(NowDateTimeProvider.Instance, TimeSpan.FromMilliseconds(20));

            if (_coreTransport.Init(this))
            {
                return _coreTransport.Start(r =>
                {
                    Fail(r, "Unexpected underlying transport stop");
                });
            }

            return false;
        }

        protected override void OnStopped(StopReason reason)
        {
            _coreTransport.Stop(reason);

            var driver = _sessionsLogicDriver;
            driver?.Finish().ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    Log.wtf(t.Exception);
                }
            });
            _sessionsLogicDriver = null;
        }

        public override int MessageMaxByteSize => _coreTransport.MessageMaxByteSize;

        IAckRawServerHandler? IRawServerAcknowledger<IAckRawServerHandler>.TryAck(UnionDataList ackData)
        {
            using var ackDataDisposer = ackData.AsDisposable();
            if (!ackData.TryPopFirst(out IMultiRefReadOnlyByteArray? ackRequest))
            {
                return null;
            }
            using var ackRequestDisposer = ackRequest.AsDisposable();
            if (!ackRequest.EqualByContent(ReconnectableInfo.AckRequest))
            {
                return null;
            }

            if (!ackData.TryPopFirst(out int id) || !ackData.TryPopFirst(out int generation))
            {
                return null;
            }
            
            SessionId sessionId = new SessionId(id, generation);
            if (sessionId.IsValid) // Пытаемся продолжить конкретную сессию
            {
                if (!ackData.TryPopFirst(out IMultiRefReadOnlyByteArray? secret))
                {
                    return null;
                }
                using var secretDisposer = secret.AsDisposable();
                
                var logic = _sessionsMap.Find(sessionId);
                if (logic != null) // Нашли искомую сессию
                {
                    if (logic.Reattach(secret.Acquire())) // Ломимся в неё
                    {
                        return logic;
                    }

                    // Сессия есть, но она нас реджектит (у клиента будет не аксепт)
                    Log.w("Session[{0}] rejected user reconnection", sessionId);
                    return null;
                }

                Log.w("Session[{0}] not found", sessionId);
                return null;
            }

            IAckRawServerHandler? userHandler = TryConnectNewClient(ackData.Acquire());
            if (userHandler != null)
            {
                ReconnectableServerLogic logic = new ReconnectableServerLogic(userHandler, _disconnectTimeout, Log, Memory);

                logic.OnConnected += (endPoint) => { userHandler.OnConnected(endPoint); };

                logic.OnStopped += (reason) =>
                {
                    Log.i("{0} is closed", logic);
                    userHandler.OnDisconnected(reason);
                    _sessionsMap.RemoveSession(logic.Id);
                };

                sessionId = _sessionsMap.AddSession(logic);
                if (sessionId.IsValid)
                {
                    logic.Attach(sessionId);
                    if (_sessionsLogicDriver!.Start(logic) != LogicStartResult.Success)
                    {
                        _sessionsMap.RemoveSession(logic.Id);
                        Log.w("{0} failed to start", logic);
                        return null;
                    }
                    return logic;
                }
            }

            return null;
        }

        public override string ToString()
        {
            return "reconnectable-server";
        }
    }
}
