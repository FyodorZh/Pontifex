// using System;
// using System.Net.Sockets;
// using Actuarius.Collections;
// using Actuarius.Memory;
// using Operarius;
// using Pontifex.NetSockets;
// using Pontifex.Raw.Reliable.Ack.Tcp;
// using Pontifex.Utils;
//
// namespace Pontifex.Raw.Reliable.Ack
// {
//     internal class RawReliableAckTcpEndpoint : RawReliableAckClientEndpoint
//     {
//         private readonly IpEndPoint _endPoint;
//         private readonly IRawReliableAckClientHandler _handler;
//         
//         private Socket? _socket;
//         
//         private TcpReceiver? mSocketReceiver;
//         private TcpSender? mSocketSender;
//
//         public RawReliableAckTcpEndpoint(
//             IRawReliableTransport owner, 
//             IpEndPoint remoteEndPoint, 
//             IRawReliableAckClientHandler handler) 
//             : base(
//                 owner, 
//                 remoteEndPoint, 
//                 handler, 
//                 TcpInfo.DefaultMessageMaxSize, 
//                 TcpInfo.DefaultBufferCapacity)
//         {
//             _endPoint = remoteEndPoint;
//             _handler = handler;
//         }
//
//         protected override bool BeginConnect()
//         {
//             try
//             {
//                 _socket = new Socket(_endPoint.EP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
//
//                 _socket.ReceiveTimeout = (int)TcpInfo.DefaultDisconnectTimeout.TotalMilliseconds;
//                 _socket.SendTimeout = (int)TcpInfo.DefaultDisconnectTimeout.TotalMilliseconds;
//                 _socket.NoDelay = true;
//
//                 UnionDataList ackData = Memory.CollectablePool.Acquire<UnionDataList>();
//                 _handler.FillAckData(ackData);
//                 ackData.PutFirst(TcpInfo.AckRequest);
//
//                 _socket.BeginConnect(_endPoint.EP, ConnectCallback, ackData);
//
//                 mLastMessageReceiveTime.Time = DateTime.UtcNow;
//                 try
//                 {
//                     TimeSpan keepAlivePeriod = TimeSpan.FromMilliseconds(1000);
//
//                     mKeepAliver = new KeepAliver(this, Memory);
//
//                     var driver = new SingleJobLogicDriver<IPeriodicLogicDriverCtl>(
//                         new ThreadBasedPeriodicMultiLogicDriver(NowDateTimeProvider.Instance, keepAlivePeriod));
//                     if (driver.Start(mKeepAliver) != LogicStartResult.Success)
//                     {
//                         throw new Exception("Couldn't start mKeepAliverSharedLogicRunner");
//                     }
//                 }
//                 catch (Exception ex)
//                 {
//                     Log.wtf(ex);
//                     return false;
//                 }
//
//                 return true;
//             }
//             catch (Exception ex1)
//             {
//                 Log.wtf(ex1);
//                 mState = State.Disconnected;
//             }
//         }
//         
//         private void ConnectCallback(IAsyncResult ar)
//         {
//             try
//             {
//                 var socket = _socket ?? throw new Exception("Socket is null");
//                 socket.EndConnect(ar);
//
//                 mSocketReceiver = new TcpReceiver(socket, OnReceived, OnFailed, null, 
//                     MessageMaxByteSize, Memory, Log, this);
//                 mSocketReceiver.Start();
//
//                 mSocketSender = new TcpSender(socket, MessageMaxByteSize, mSocket.SendBufferSize - 4, Memory, Log);
//                 mSocketSender.ErrorOccured += OnFailed;
//                 mSocketSender.Stopped += () =>
//                 {
//                     try
//                     {
//                         mSocket?.Shutdown(SocketShutdown.Both);
//                         mSocket?.Close();
//                         mSocket = null;
//                     }
//                     catch (Exception)
//                     {
//                         // ignored
//                     }
//
//                     Log.i("Stopped.");
//                 };
//                 ILogicDriver<INonPeriodicLogicDriverCtl> driver =
//                     new SingleJobLogicDriver<INonPeriodicLogicDriverCtl>(new ThreadBasedNonPeriodicLogicMultiDriver(NowDateTimeProvider.Instance));
//                 driver.Start(mSocketSender);
//
//                 ConnectionState = State.Connecting;
//
//                 var ackData = (UnionDataList)ar.AsyncState;
//                 var sendResult = DoSend(PacketType.AckRequest, ackData);
//                 if (sendResult != SendResult.Ok)
//                 {
//                     Log.w("AckRequest send failed: {0}", sendResult);
//                     Stop(new StopReasons.TextFail(Name, "AckRequest send failed: {0}", sendResult));
//                 }
//             }
//             catch (Exception ex)
//             {
//                 Stop(new StopReasons.ExceptionFail(Name, ex, "ConnectCallback failed"));
//                 var ackData = (UnionDataList)ar.AsyncState;
//                 ackData.Release();
//             }
//         }
//         
//         private void OnReceived(UnionDataList packet)
//         {
//             using var packetDisposer = packet.AsDisposable();
//
//             PacketType packetType;
//             if (packet.TryPopFirst(out byte packetTypeByte))
//             {
//                 packetType = (PacketType)packetTypeByte;
//             }
//             else
//             {
//                 string text = $"Failed to parse incoming message type";
//                 Log.e(text);
//                 Stop(new StopReasons.TextFail(Name, text));
//                 return;
//             }
//
//             mLastMessageReceiveTime.Time = DateTime.UtcNow;
//             switch (ConnectionState)
//             {
//                 case RawReliableAckTcpClient.State.Connecting:
//                     try
//                     {
//                         if (packetType == PacketType.AckResponse)
//                         {
//                             if (packet.TryPopFirst(out IMultiRefReadOnlyByteArray? ackOk))
//                             {
//                                 using var ackOkDisposer = ackOk.AsDisposable();
//                                 if (TcpInfo.AckOKResponse.EqualByContent(ackOk))
//                                 {
//                                     ConnectionState = RawReliableAckTcpClient.State.Connected;
//                                     ConnectionFinished(this, packet.Acquire());
//                                     break;
//                                 }
//                             }
//                             Log.w("Failed to parse ack response. Disconnecting...");
//                             Stop(new StopReasons.AckRejected(Name));
//                         }
//                         else if (packetType == PacketType.Disconnect)
//                         {
//                             Log.w("Failed to Ack on server. Disconnecting...");
//                             Stop(new StopReasons.AckRejected(Name));
//                         }
//                         else
//                         {
//                             Stop(new StopReasons.TextFail(Name, "Wrong first message type. Expected '{0}', received '{1}'", PacketType.AckResponse, packetType));
//                         }
//                     }
//                     catch (Exception ex)
//                     {
//                         Stop(new StopReasons.ExceptionFail(Name, ex, ""));
//                     }
//                     break;
//                 case RawReliableAckTcpClient.State.Connected:
//                     if (packetType == PacketType.Regular)
//                     {
//                         mTrafficCollector.IncInTraffic(packet.GetDataSize());
//
//                         var handler = Handler;
//                         if (handler != null)
//                         {
//                             try
//                             {
//                                 handler.OnReceived(packet.Acquire());
//                             }
//                             catch (Exception ex)
//                             {
//                                 Log.e("User logic exception, continue working...\n{0}", ex);
//                             }
//                         }
//                     }
//                     else if (packetType == PacketType.Disconnect)
//                     {
//                         Stop(new StopReasons.UnknownRemoteIntention(Name));
//                     }
//                     else if (packetType == PacketType.Ping)
//                     {
//                         try
//                         {
//                             if (packet.TryPopFirst(out long data))
//                             {
//                                 DateTime time = DateTime.FromBinary(data);
//                                 DateTime now = DateTime.UtcNow;
//                                 int pingMs = (int)((now - time).TotalMilliseconds + 0.5f);
//                                 mPingCollector.SetPing(pingMs);
//                             }
//                             else
//                             {
//                                 throw new Exception("Bad ping message");
//                             }
//                         }
//                         catch (Exception ex)
//                         {
//                             Log.e("Failed to process ping response.\n{0}", ex);
//                         }
//                     }
//                     else
//                     {
//                         Stop(new StopReasons.TextFail(Name, "Wrong incoming packet type. Received '{0}'", packetType));
//                     }
//
//                     break;
//             }
//         }
//
//         protected override void DoSend(UnionDataList bufferToSend)
//         {
//             throw new NotImplementedException();
//         }
//     }
// }