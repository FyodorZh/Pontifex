// using System;
// using Shared;
// using Shared.Buffer;
// using Shared.FSM;
// using Transport.Abstractions;
// using Transport.Abstractions.Endpoints.Client;
// using Transport.Abstractions.Handlers.Client;
// using Transport.StopReasons;
// using TimeSpan = System.TimeSpan;
//
// namespace Transport.Protocols.Reliable.AckRaw
// {
//     internal class ReliableClientProtocol : ReliableProtocol, IAckRawServerEndpoint
//     {
//         private class RemoteEp : IRemoteEndPoint
//         {
//             private readonly INoAckUnreliableRawServerEndpoint mRemoteEp;
//
//             public RemoteEp(INoAckUnreliableRawServerEndpoint remoteEp)
//             {
//                 mRemoteEp = remoteEp;
//             }
//
//             public IEndPoint RemoteEndPoint
//             {
//                 get { return mRemoteEp.ServerEndPoint; }
//             }
//
//             public int MessageMaxByteSize
//             {
//                 get { return mRemoteEp.MessageMaxByteSize; }
//             }
//
//             public SendResult Send(IMacroOwner<Message> messages)
//             {
//                 return mRemoteEp.Send(messages);
//             }
//         }
//
//         private enum ProtocolState
//         {
//             Constructed,
//             WaitingForAck,
//             Connected,
//             Stopped
//         }
//
//         private readonly IConcurrentFSM<ProtocolState> mState;
//
//
//         private readonly IAckRawClientHandler mUserHandler;
//         private readonly Action<StopReason> mOnStopped;
//
//         public ReliableClientProtocol(
//             INoAckUnreliableRawServerEndpoint serverEp,
//             TimeSpan disconnectTimeout,
//             IAckRawClientHandler userHandler,
//             Action<StopReason> onStopped,
//             ILogger logger)
//             : base(new RemoteEp(serverEp), disconnectTimeout, UtcNowDateTimeProvider.Instance, logger, null)
//         {
//             mUserHandler = userHandler;
//             mOnStopped = onStopped;
//
//             var fsm = new FSM<ProtocolState, int>(ProtocolState.Constructed, state => (int)state);
//             fsm.AddTransition(ProtocolState.Constructed, ProtocolState.WaitingForAck);
//             fsm.AddTransition(ProtocolState.WaitingForAck, ProtocolState.Connected);
//             fsm.AddTransitions(new []{ ProtocolState.Constructed, ProtocolState.WaitingForAck, ProtocolState.Connected}, ProtocolState.Stopped);
//
//             mState = new ConcurrentFSM<ProtocolState>(fsm);
//         }
//
//         protected override string ProtocolName
//         {
//             get { return "Reliable.Client"; }
//         }
//
//         protected override bool OnLogicStarted()
//         {
//             return true;
//         }
//
//         protected override void OnTick()
//         {
//             switch (mState.State)
//             {
//                 case ProtocolState.Constructed:
//                     mState.SetState(ProtocolState.WaitingForAck, (state, newState) =>
//                     {
//                         if (state == ProtocolState.Constructed)
//                         {
//                             using (var bufferAccessor = ConcurrentUsageMemoryBufferPool.Instance.Allocate().ExposeAccessorOnce())
//                             {
//                                 bufferAccessor.Buffer.PushArray(new ByteArraySegment(mUserHandler.WriteAckData()));
//                                 bufferAccessor.Buffer.PushArray(new ByteArraySegment(ReliableInfo.AckPrefix));
//                                 Send(bufferAccessor.Acquire());
//                             }
//                             return true;
//                         }
//                         return false;
//                     });
//                     break;
//                 case ProtocolState.WaitingForAck:
//                     // do nothing
//                     break;
//                 case ProtocolState.Connected:
//                     break;
//                 case ProtocolState.Stopped:
//                     break;
//                 default:
//                     throw new ArgumentOutOfRangeException();
//             }
//         }
//
//         protected override void OnStopped(StopReason reason)
//         {
//             mState.SetState(ProtocolState.Stopped, (state, newState) =>
//             {
//                 if (state == ProtocolState.Connected)
//                 {
//                     mUserHandler.OnDisconnected(reason);
//                 }
//                 mUserHandler.OnStopped(reason);
//                 mOnStopped(reason);
//                 return true;
//             });
//         }
//
//         protected override void OnReceived(IMemoryBufferHolder buffer)
//         {
//             using (var bufferAccessor = buffer.ExposeAccessorOnce())
//             {
//                 switch (mState.State)
//                 {
//                     case ProtocolState.Constructed:
//                         mState.SetState(ProtocolState.Stopped);
//                         Fail("Protocol violation (1)");
//                         break;
//                     case ProtocolState.WaitingForAck:
//                         ByteArraySegment ackResponseHeader;
//                         if (bufferAccessor.Buffer.PopFirst().AsArray(out ackResponseHeader) &&
//                             ackResponseHeader.EqualByContent(new ByteArraySegment(ReliableInfo.AckOKResponse)))
//                         {
//                             ByteArraySegment ackResponse;
//                             if (bufferAccessor.Buffer.PopFirst().AsArray(out ackResponse))
//                             {
//                                 mState.SetState(ProtocolState.Connected, (state, newState) =>
//                                 {
//                                     if (state != ProtocolState.WaitingForAck)
//                                     {
//                                         return false;
//                                     }
//                                     mUserHandler.OnConnected(this, new ByteArraySegment(ackResponse.Clone()));
//                                     return true;
//                                 });
//                             }
//                         }
//                         else
//                         {
//                             mState.SetState(ProtocolState.Stopped);
//                             Fail("Protocol violation (2)");
//                         }
//                         break;
//                     case ProtocolState.Connected:
//                         try
//                         {
//                             mUserHandler.OnReceived(bufferAccessor.Acquire());
//                         }
//                         catch (Exception e)
//                         {
//                             mState.SetState(ProtocolState.Stopped);
//                             Fail(new ExceptionFail(ProtocolName, e));
//                         }
//                         break;
//                     case ProtocolState.Stopped:
//                         // Ignore
//                         break;
//                     default:
//                         throw new ArgumentOutOfRangeException();
//                 }
//             }
//         }
//     }
// }