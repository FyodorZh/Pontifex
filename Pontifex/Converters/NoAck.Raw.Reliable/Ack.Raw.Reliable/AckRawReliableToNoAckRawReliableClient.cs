// using System;
// using Actuarius.Memory;
// using Pontifex.Ack.Raw;
// using Pontifex.NoAck.Raw;
// using Pontifex.StopReasons;
// using Pontifex.Utils;
// using Scriba;
//
// namespace Pontifex.Converters
// {
//     public sealed class AckRawReliableToNoAckRawReliableClient : AnyTransport, INoAckRawReliableClient, IAckRawReliableClientHandler
//     {
//         private readonly object _stateLock = new();
//         private readonly Func<IAckRawReliableClient?> _innerFactory;
//
//         private IAckRawReliableClient? _innerTransport;
//         private IAckRawReliableClientSideEndpoint? _endpoint;
//         private bool _userStopped;
//         private bool _reconnectionHandled;
//
//         public AckRawReliableToNoAckRawReliableClient(
//             Func<IAckRawReliableClient?> innerFactory,
//             string typeName,
//             ILogger logger,
//             IMemoryRental memory)
//             : base(typeName, logger, memory)
//         {
//             _innerFactory = innerFactory;
//         }
//
//         public override TransportType Type => TransportType.NoAckRawReliable;
//
//         public event Action<UnionDataList>? OnReceived;
//
//         public int MessageMaxByteSize
//         {
//             get
//             {
//                 lock (_stateLock)
//                 {
//                     return _endpoint?.MessageMaxByteSize ?? _innerTransport?.MessageMaxByteSize ?? 0;
//                 }
//             }
//         }
//
//         public SendResult Send(UnionDataList message)
//         {
//             IAckRawReliableClientSideEndpoint? endpoint;
//
//             lock (_stateLock)
//             {
//                 endpoint = _endpoint;
//                 if (!IsStarted || endpoint == null)
//                 {
//                     message.Release();
//                     return SendResult.NotConnected;
//                 }
//             }
//
//             return endpoint.Send(message);
//         }
//
//         #region AnyTransport overrides
//
//         protected override bool TryStart()
//         {
//             var transport = CreateAndStartInner();
//             if (transport == null)
//                 return false;
//
//             lock (_stateLock)
//             {
//                 _innerTransport = transport;
//             }
//             return true;
//         }
//
//         protected override void OnStarted()
//         {
//         }
//
//         protected override void OnStopped(StopReason reason)
//         {
//             lock (_stateLock)
//             {
//                 _userStopped = true;
//             }
//
//             var transport = ClearInner();
//             transport?.Stop(reason);
//         }
//
//         #endregion
//
//         #region IAckRawReliableClientHandler
//
//         void IAckRawReliableClientHandler.OnConnected(IAckRawReliableClientSideEndpoint endPoint, UnionDataList ackResponse)
//         {
//             ackResponse.Release();
//
//             lock (_stateLock)
//             {
//                 _endpoint = endPoint;
//             }
//
//             Log.i("Connected");
//         }
//
//         void IAckRawBaseHandler.OnDisconnected(StopReason reason)
//         {
//             lock (_stateLock)
//             {
//                 _endpoint = null;
//             }
//
//             Log.i("Disconnected: {0}", reason);
//         }
//
//         void IAckRawClientHandler.OnStopped(StopReason reason)
//         {
//             TryReconnect(reason);
//         }
//
//         void IAckRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
//         {
//             var handler = OnReceived;
//             if (handler != null)
//             {
//                 try
//                 {
//                     handler(receivedBuffer);
//                 }
//                 catch (Exception ex)
//                 {
//                     Log.wtf(ex);
//                 }
//             }
//             else
//             {
//                 receivedBuffer.Release();
//             }
//         }
//
//         void IAckRawClientHandler.FillAckData(UnionDataList ackData)
//         {
//         }
//
//         #endregion
//
//         private bool TryReconnect(StopReason reason)
//         {
//             lock (_stateLock)
//             {
//                 if (_userStopped)
//                     return false;
//
//                 if (_reconnectionHandled)
//                     return true;
//
//                 _reconnectionHandled = true;
//                 _innerTransport = null;
//                 _endpoint = null;
//             }
//
//             var transport = CreateAndStartInner();
//             if (transport == null)
//             {
//                 Log.e("Reconnection failed, stopping transport");
//
//                 lock (_stateLock)
//                 {
//                     _reconnectionHandled = false;
//                 }
//
//                 Stop(new ChainFail(Name, reason, "Reconnection failed"));
//                 return false;
//             }
//
//             lock (_stateLock)
//             {
//                 _reconnectionHandled = false;
//
//                 if (_userStopped)
//                 {
//                     transport.Stop(new Unknown(Name));
//                     return false;
//                 }
//
//                 _innerTransport = transport;
//             }
//
//             return true;
//         }
//
//         private IAckRawReliableClient? CreateAndStartInner()
//         {
//             var transport = _innerFactory();
//             if (transport == null)
//             {
//                 Log.e("Factory returned null");
//                 return null;
//             }
//
//             if (!transport.Init(this))
//             {
//                 Log.e("Failed to init inner transport");
//                 return null;
//             }
//
//             if (!transport.Start(OnInnerStopped))
//             {
//                 Log.e("Failed to start inner transport");
//                 return null;
//             }
//
//             return transport;
//         }
//
//         private IAckRawReliableClient? ClearInner()
//         {
//             lock (_stateLock)
//             {
//                 var transport = _innerTransport;
//                 _innerTransport = null;
//                 _endpoint = null;
//                 return transport;
//             }
//         }
//
//         private void OnInnerStopped(StopReason reason)
//         {
//             TryReconnect(reason);
//         }
//
//         public override string ToString()
//         {
//             return $"{Name}<inner-transport>";
//         }
//     }
// }
