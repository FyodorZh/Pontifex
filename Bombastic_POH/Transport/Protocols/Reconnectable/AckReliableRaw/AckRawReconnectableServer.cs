using Shared;
using Shared.Utils;
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

        private readonly SessionMap<ReconnectableServerLogic> mSessionsMap = new SessionMap<ReconnectableServerLogic>(ReconnectableInfo.ServerConnectionsLimit);
        private PeriodicMultiLogicMultiDriver mSessions;

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
            if (driver != null)
            {
                driver.Stop();
            }
            mSessions = null;
        }

        public override int MessageMaxByteSize
        {
            get { return mCoreTransport.MessageMaxByteSize; }
        }

        IAckRawServerHandler IRawServerAcknowledger<IAckRawServerHandler>.TryAck(ByteArraySegment ackData, ILogger logger)
        {
            ackData = AckUtils.CheckPrefix(ackData, ReconnectableInfo.AckRequest);
            if (ackData.IsValid && ackData.Count > 1)
            {
                ByteArraySegment sessionBytes;
                SessionId sessionId;

                ackData = AckUtils.GetPrefix(ackData, ackData[0], out sessionBytes);
                if (ackData.IsValid && SessionId.TryDeserialize(out sessionId, sessionBytes))
                {
                    if (sessionId.IsValid) // Пытаемся продолжить конкретную сессию
                    {
                        var logic = mSessionsMap.Find(sessionId);
                        if (logic != null) // Нашли искомую сессию
                        {
                            if (logic.Reattach(ackData)) // Ломимся в неё
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

                    IAckRawServerHandler userHandler = TryConnectNewClient(ackData);
                    if (userHandler != null)
                    {
                        ReconnectableServerLogic logic = new ReconnectableServerLogic(userHandler, mDisconnectTimeout);

                        logic.OnConnected += (endPoint) =>
                            {
                                userHandler.OnConnected(endPoint);
                            };

                        logic.OnStopped += (reason) =>
                            {
                                Log.i("{0} is closed", logic);
                                userHandler.OnDisconnected(reason);
                                mSessionsMap.RemoveSession(logic.Id);
                            };

                        sessionId = mSessionsMap.AddSession(logic);
                        if (sessionId.IsValid)
                        {
                            logic.Attach(sessionId, ackData);
                            mSessions.Append(logic, DeltaTime.FromMiliseconds(20));
                            return logic;
                        }
                    }
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
