using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Operarius;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Reconnectable
{
    public class RawReliableAckReconnectableServer : RawReliableAckServer, IRawReliableAckServer, IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>
    {
        private readonly IRawReliableAckServer _coreTransport;
        private readonly TimeSpan _disconnectTimeout;

        private readonly SessionMap<ReconnectableServerLogic> _sessionsMap = new (ReconnectableInfo.ServerConnectionsLimit);
        private ILogicDriver<IPeriodicLogicDriverCtl>? _sessionsLogicDriver;

        public RawReliableAckReconnectableServer(IRawReliableAckServer coreTransport, TimeSpan disconnectTimeout, ILogger logger, IMemoryRental memoryRental)
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

        (IRawReliableAckServerHandler handler, UnionDataList ackResponse)? IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>.TryAck(IEndPoint remote, UnionDataList ackData)
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
                    // Reattach answers with the response composed for the original connection, so the
                    // clone has to exist before the session is claimed.
                    var clonedResponse = logic.CloneAckResponse();
                    if (clonedResponse == null)
                    {
                        Log.w("Session[{0}] has no ACK response to reattach with", sessionId);
                        return null;
                    }

                    if (!logic.Reattach(secret.Acquire())) // Ломимся в неё
                    {
                        clonedResponse.Release();

                        // Сессия есть, но она нас реджектит (у клиента будет не аксепт)
                        Log.w("Session[{0}] rejected user reconnection", sessionId);
                        return null;
                    }

                    return (logic, clonedResponse);
                }

                Log.w("Session[{0}] not found", sessionId);
                return null;
            }

            var newClient = TryConnectNewClient(remote, ackData.Acquire());
            if (newClient == null)
            {
                return null;
            }

            IRawReliableAckServerHandler userHandler = newClient.Value.handler;
            UnionDataList userAckResponse = newClient.Value.ackResponse;

            ReconnectableServerLogic newLogic = new ReconnectableServerLogic(userHandler, _disconnectTimeout, Log, Memory);

            newLogic.OnConnected += (endPoint) => { userHandler.OnConnected(endPoint); };

            newLogic.OnStopped += (reason) =>
            {
                Log.i("{0} is closed", newLogic);
                userHandler.OnDisconnected(reason);
                _sessionsMap.RemoveSession(newLogic.Id);
                newLogic.ReleaseAckResponse();
            };

            sessionId = _sessionsMap.AddSession(newLogic);
            if (!sessionId.IsValid)
            {
                userAckResponse.Release();
                NotifyUserHandlerStopped(userHandler, "Session map is full");
                return null;
            }

            newLogic.Attach(sessionId);

            UnionDataList composedResponse;
            try
            {
                composedResponse = newLogic.CreateAckResponse(userAckResponse);
            }
            catch
            {
                _sessionsMap.RemoveSession(newLogic.Id);
                newLogic.ReleaseAckResponse();
                NotifyUserHandlerStopped(userHandler, "Failed to compose the ACK response");
                throw;
            }

            // The logic driver must not run before the session can answer a reattach.
            if (_sessionsLogicDriver!.Start(newLogic) != LogicStartResult.Success)
            {
                _sessionsMap.RemoveSession(newLogic.Id);
                newLogic.ReleaseAckResponse();
                composedResponse.Release();
                Log.w("{0} failed to start", newLogic);
                NotifyUserHandlerStopped(userHandler, "Failed to start the session logic");
                return null;
            }

            return (newLogic, composedResponse);
        }

        private void NotifyUserHandlerStopped(IRawReliableAckServerHandler userHandler, string reason)
        {
            try
            {
                userHandler.OnStopped(new TextFail(ReconnectableInfo.TransportName, reason));
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        public override string ToString()
        {
            return "reconnectable-server";
        }
    }
}
