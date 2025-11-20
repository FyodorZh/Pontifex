// using System.Threading;
// using Shared;
// using Transport.Abstractions;
// using Transport.Abstractions.Clients;
// using Transport.Abstractions.Handlers.Client;
// using Transport.Transports.Core;
// using Shared.Utils;
//
// namespace Transport.Protocols.Reliable.AckRaw
// {
//     internal sealed class AckRawReliableClientNew : AbstractTransport, IAckReliableRawClient, INoAckUnreliableRawClientHandler
//     {
//         private readonly INoAckUnreliableRawClient mBaseTransport;
//         private readonly System.TimeSpan mDisconnectTimeout;
//
//         private readonly IPeriodicLogicRunner mLogicRunner;
//
//         private volatile ReliableClientProtocol mProtocol;
//
//         private IAckRawClientHandler mUserHandler;
//
//
//         public AckRawReliableClientNew(INoAckUnreliableRawClient baseTransport, System.TimeSpan disconnectionTimeout, IPeriodicLogicRunner logicRunner)
//         {
//             mLogicRunner = logicRunner;
//
//             mBaseTransport = baseTransport;
//             AppendControl(baseTransport);
//
//             mDisconnectTimeout = disconnectionTimeout;
//         }
//
//         public override string Type
//         {
//             get { return ReliableInfo.TransportName; }
//         }
//
//         bool IAckRawClient.Init(IAckRawClientHandler handler)
//         {
//             if (mUserHandler == null && handler != null)
//             {
//                 mUserHandler = handler.Test(text => Log.e(text));
//                 if (mBaseTransport.Init(this))
//                 {
//                     return true;
//                 }
//             }
//             mUserHandler = null;
//             return false;
//         }
//
//         protected override bool TryStart()
//         {
//             return mBaseTransport.Start(r =>
//             {
//                 if (IsStarted)
//                 {
//                     Fail(r, "Unexpected underlying transport stop");
//                 }
//             }, Log);
//         }
//
//         protected override void OnStarted()
//         {
//             // DO NOTHING
//         }
//
//         protected override void OnStopped(StopReason reason)
//         {
//             var protocol = Interlocked.Exchange(ref mProtocol, null);
//             if (protocol != null)
//             {
//                 protocol.Stop(reason);
//             }
//
//             mBaseTransport.Stop(reason);
//         }
//
//         void INoAckUnreliableRawClientHandler.OnStarted(INoAckUnreliableRawServerEndpoint endpoint)
//         {
//             if (endpoint == null)
//             {
//                 Fail("INoAckReliableRRClientHandler.Started", "Underlying transport endpoint is null");
//                 return;
//             }
//
//             var handler = mUserHandler;
//             if (handler == null)
//             {
//                 Fail("INoAckReliableRRClientHandler.Started", "User handler is null. Did you invoke Init() method?");
//                 return;
//             }
//
//             var protocol = new ReliableClientProtocol(endpoint, mDisconnectTimeout, handler, reason => Stop(reason), mBaseTransport.Log);
//
//             bool started;
//             if (mLogicRunner != null)
//             {
//                 started = mLogicRunner.Run(protocol, DeltaTime.FromMiliseconds(ReliableInfo.LogicClientQuantLength)) != null;
//             }
//             else
//             {
//                 PeriodicLogicThreadedDriver driver = new PeriodicLogicThreadedDriver(DeltaTime.FromMiliseconds(ReliableInfo.LogicClientQuantLength), 128);
//                 started = driver.Start(protocol, mBaseTransport.Log);
//             }
//
//             if (started)
//             {
//                 mProtocol = protocol;
//                 PingCollector ping = new PingCollector(ReliableInfo.TransportName);
//                 protocol.Ping = ping;
//                 AppendControl(ping);
//             }
//             else
//             {
//                 Fail("INoAckReliableRRClientHandler.Started", "Failed to start protocol logic");
//             }
//         }
//
//         void INoAckUnreliableRawClientHandler.OnReceived(Message message)
//         {
//             var protocol = mProtocol;
//             if (protocol != null)
//             {
//                 protocol.Receive(message);
//             }
//             else
//             {
//                 message.Release();
//             }
//         }
//
//         void INoAckUnreliableRawClientHandler.OnStopped()
//         {
//             Stop();
//         }
//
//         public int MessageMaxByteSize
//         {
//             get
//             {
//                 var protocol = mProtocol;
//                 if (protocol != null)
//                 {
//                     return protocol.MessageMaxByteSize;
//                 }
//                 return 0;
//             }
//         }
//
//         public override string ToString()
//         {
//             return "RELIABLE";
//         }
//     }
// }
