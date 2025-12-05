using Actuarius.Collections;
using Actuarius.Memory;
using Actuarius.PeriodicLogic;
using Pontifex.Utils;
using Transport.Abstractions.Acknowledgers;
using Transport.Abstractions.Handlers.Server;
using Transport.Abstractions.Servers;
using Transport.Transports.Core;

namespace Transport.Protocols.Reconnectable.AckReliableRaw
{
    public class AckRawReconnectableServer : AckRawServer, IAckReliableRawServer, IRawServerAcknowledger<IAckRawServerHandler>
    {
        private readonly IAckReliableRawServer mCoreTransport;
        private readonly System.TimeSpan mDisconnectTimeout;

        private readonly SessionMap<ReconnectableServerLogic> mSessionsMap = new (ReconnectableInfo.ServerConnectionsLimit);
        private PeriodicMultiLogicMultiDriver? mSessions;

        public AckRawReconnectableServer(IAckReliableRawServer coreTransport, System.TimeSpan disconnectTimeout)
            : base(ReconnectableInfo.TransportName)
        {
            mCoreTransport = coreTransport;
            mDisconnectTimeout = disconnectTimeout;
        }

        protected override bool TryStart()
        {
            int threads = 2;
            IPeriodicLogicDriver[] drivers = new IPeriodicLogicDriver[threads];
            for (int i = 0; i < threads; ++i)
            {
                drivers[i] = new PeriodicLogicThreadedDriver(DeltaTime.FromMiliseconds(20), 128);
            }
            mSessions = new PeriodicMultiLogicMultiDriver(drivers);
            mSessions.Start(Log);

            if (mCoreTransport.Init(this))
            {
                return mCoreTransport.Start(r =>
                {
                    Fail(r, "Unexpected underlying transport stop");
                }, Log);
            }

            return false;
        }

        protected override void OnStopped(StopReason reason)
        {
            mCoreTransport.Stop(reason);

            var driver = mSessions;
            driver?.Stop();
            mSessions = null;
        }

        public override int MessageMaxByteSize => mCoreTransport.MessageMaxByteSize;

        public void Setup(IMemoryRental memory, ILogger logger)
        {
            // TODO?
        }

        IAckRawServerHandler? IRawServerAcknowledger<IAckRawServerHandler>.TryAck(UnionDataList ackData)
        {
            using var ackDataDisposer = ackData.AsDisposable();
            if (!ackData.PopFirstAsArray(out var ackRequest))
            {
                return null;
            }

            using var ackRequestDisposer = ackRequest.AsDisposable();

            if (!ackRequest.EqualByContent(ReconnectableInfo.AckRequest))
            {
                return null;
            }

            UnionData r2 = default;
            if (!ackData.TryGetFirst(out var r1) || !ackData.TryGetFirst(out r2) ||
                r1.Type != UnionDataType.Int || r2.Type != UnionDataType.Int)
            {
                r1.Bytes?.Release();
                r2.Bytes?.Release();
                return null;
            }

            SessionId sessionId = new SessionId(r1.Alias.IntValue, r2.Alias.IntValue);
            if (sessionId.IsValid) // Пытаемся продолжить конкретную сессию
            {
                var logic = mSessionsMap.Find(sessionId);
                if (logic != null) // Нашли искомую сессию
                {
                    if (logic.Reattach(ackData.Acquire())) // Ломимся в неё
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
                ReconnectableServerLogic logic = new ReconnectableServerLogic(userHandler, mDisconnectTimeout);

                logic.OnConnected += (endPoint) => { userHandler.OnConnected(endPoint); };

                logic.OnStopped += (reason) =>
                {
                    Log.i("{0} is closed", logic);
                    userHandler.OnDisconnected(reason);
                    mSessionsMap.RemoveSession(logic.Id);
                };

                sessionId = mSessionsMap.AddSession(logic);
                if (sessionId.IsValid)
                {
                    logic.Attach(sessionId, ackData.Acquire());
                    mSessions!.Append(logic, DeltaTime.FromMiliseconds(20));
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
