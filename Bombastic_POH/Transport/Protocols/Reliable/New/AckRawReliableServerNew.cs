using System;
using System.Collections.Generic;
using Shared;
using Shared.Concurrent;
using Transport.Abstractions;
using Transport.Transports.Core;
using Shared.Utils;
using Transport.Abstractions.Acknowledgers;
using Transport.Abstractions.Controls;
using Transport.Abstractions.Handlers.Server;
using Transport.Abstractions.Servers;
using Transport.Utils;

namespace Transport.Protocols.Reliable.AckRaw
{
    internal sealed class AckRawReliableServerNew : AckRawServer, IAckReliableRawServer, INoAckUnreliableRawServerHandler, ICCUController,
        IPeriodicLogic
    {
        private struct MessageInfo
        {
            public IEndPoint Ep;
            public Message Msg;

            public MessageInfo(IEndPoint ep, Message message)
            {
                Ep = ep;
                Msg = message;
            }
        }

        private readonly INoAckUnreliableRawServer mBaseTransport;
        private readonly System.TimeSpan mDisconnectTimeout;
        private readonly ThreadSafeDateTime mTimeProvider = new ThreadSafeDateTime();

        private volatile INoAckUnreliableRawClientEndpoint mEndpoint;

        private ILogicDriverCtl mDriverCtl;

        private readonly SingleReaderWriterConcurrentQueueValve<MessageInfo> mIncomingMessages;

        private readonly Dictionary<IEndPoint, int> mSessionMap = new Dictionary<IEndPoint, int>();
        private readonly List<ReliableServerProtocol> mSessionList = new List<ReliableServerProtocol>();
        private readonly List<PeriodicLogicManualDriver> mSessionDriverList = new List<PeriodicLogicManualDriver>();

        private int mCurrentCcu;

        private readonly DeliveryController mDeliveryController;

        public AckRawReliableServerNew(INoAckUnreliableRawServer baseTransport, System.TimeSpan disconnectionTimeout)
            : base(ReliableInfo.TransportName)
        {
            mBaseTransport = baseTransport;
            AppendControl(baseTransport);
            AppendControl((ICCUController)this);
            mDisconnectTimeout = disconnectionTimeout;

            mDeliveryController = new DeliveryController(ReliableInfo.TransportName, mTimeProvider);
            AppendControl(mDeliveryController);

            mIncomingMessages = new SingleReaderWriterConcurrentQueueValve<MessageInfo>(
                new SingleReaderWriterConcurrentQueue<MessageInfo>(), (kv) => kv.Msg.Release());
        }

        protected override IRawServerAcknowledger<IAckRawServerHandler> SetupAcknowledger(IRawServerAcknowledger<IAckRawServerHandler> acknowledger)
        {
            if (acknowledger != null)
            {
                mBaseTransport.Init(this);
            }
            return base.SetupAcknowledger(acknowledger);
        }

        public override int MessageMaxByteSize
        {
            get
            {
                var ep = mEndpoint;
                if (ep != null)
                {
                    return ep.MessageMaxByteSize;
                }
                return 0;
            }
        }

        protected override bool TryStart()
        {
            var driver = new PeriodicLogicThreadedDriver(DeltaTime.FromMiliseconds(ReliableInfo.LogicServerQuantLength), 0,
                delay =>
                {
                    if (delay > ReliableInfo.LogicServerQuantLength)
                    {
                        Log.w("Reliable transport tick delay: {0}", delay);
                    }
                });

            // Стартуем свою логику
            if (!driver.Start(this, Log))
            {
                return false;
            }

            // стартуем базовый транспорт
            if (!mBaseTransport.Start(r =>
            {
                if (IsStarted)
                {
                    Fail(r, "Unexpected underlying transport stop");
                }
            }, Log))
            {
                driver.Stop();
                return false;
            }

            Log = mBaseTransport.Log;
            return true;
        }

        protected override void OnStopped(StopReason reason)
        {
            var driver = mDriverCtl;
            if (driver != null)
            {
                driver.Stop();
            }

            mBaseTransport.Stop(reason);
        }

        void INoAckUnreliableRawServerHandler.OnStarted(INoAckUnreliableRawClientEndpoint endpoint)
        {
            mEndpoint = endpoint;
        }

        void INoAckUnreliableRawServerHandler.OnStopped()
        {
            mEndpoint = null;
        }

        void INoAckUnreliableRawServerHandler.OnReceived(IEndPoint sender, Message message)
        {
            mIncomingMessages.Put(new MessageInfo(sender, message));
        }

        bool IPeriodicLogic.LogicStarted(ILogicDriverCtl driver)
        {
            mDriverCtl = driver;
            return true;
        }

        void IPeriodicLogic.LogicTick()
        {
            var endPoint = mEndpoint;
            if (endPoint == null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            mTimeProvider.Time = now;

            mDeliveryController.Refresh();

            MessageInfo msg;
            while (mIncomingMessages.TryPop(out msg))
            {
                int sessionId;
                if (mSessionMap.TryGetValue(msg.Ep, out sessionId))
                {
                    var session = mSessionList[sessionId];
                    session.Receive(msg.Msg);
                }
                else
                {
                    if (msg.Msg.Id.Id == 1)
                    {
                        var sessionLogger = Log.Wrap("Session", msg.Ep.ToString());

                        var newSession = new ReliableServerProtocol(
                            endPoint,
                            msg.Ep,
                            mDisconnectTimeout,
                            msg.Msg,
                            TryConnectNewClient,
                            mTimeProvider,
                            sessionLogger,
                            mDeliveryController);

                        if (newSession.IsAcknowledged)
                        {
                            var driver = new PeriodicLogicManualDriver(DeltaTime.Zero);
                            if (driver.Start(newSession, sessionLogger))
                            {
                                mSessionList.Add(newSession);
                                mSessionDriverList.Add(driver);
                                mSessionMap.Add(msg.Ep, mSessionList.Count - 1);
                            }
                        }
                    }
                    else
                    {
                        msg.Msg.Release();
                    }
                }
            }

            for (int i = 0; i < mSessionList.Count; ++i)
            {
                if (!mSessionDriverList[i].Tick())
                {
                    int count = mSessionList.Count;

                    mSessionMap.Remove(mSessionList[i].RemoteEndPoint);

                    if (i != count - 1)
                    {
                        mSessionList[i] = mSessionList[count - 1];
                        mSessionDriverList[i] = mSessionDriverList[count - 1];
                        mSessionMap[mSessionList[i].RemoteEndPoint] = i;
                    }

                    mSessionDriverList.RemoveAt(count - 1);
                    mSessionList.RemoveAt(count - 1);

                    --i;
                }
            }

            mCurrentCcu = mSessionList.Count;
        }

        void IPeriodicLogic.LogicStopped()
        {
            mDriverCtl = null;
            mIncomingMessages.CloseValve();
            mBaseTransport.Stop();
            mCurrentCcu = 0;
        }

        string IControl.Name
        {
            get { return Type; }
        }

        int ICCUController.CCU
        {
            get { return mCurrentCcu; }
        }
    }
}