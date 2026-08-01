// using System;
// using System.Threading;
// using Pontifex.Utils;
// using Pontifex.VirtualDelivery;
//
// namespace Pontifex.NoAck.Raw.Reliable.Direct
// {
//     public sealed class Channel : IDisposable
//     {
//         private readonly IEndPoint _clientEp;
//         private readonly IEndPoint _serverEp;
//         private volatile Action<UnionDataList>? _clientHandler;
//         private volatile Action<IEndPoint, UnionDataList>? _serverHandler;
//         private volatile IDeliverySystem _clientDeliverySystem;
//         private volatile IDeliverySystem _serverDeliverySystem;
//         private volatile bool _disposed;
//
//         public Channel(IEndPoint clientEp, IEndPoint serverEp)
//         {
//             _clientEp = clientEp;
//             _serverEp = serverEp;
//             _clientDeliverySystem = new PerfectDeliverySystem();
//             _serverDeliverySystem = new PerfectDeliverySystem();
//         }
//
//         public IEndPoint ClientEp => _clientEp;
//
//         public Action<UnionDataList>? ClientHandler
//         {
//             set => _clientHandler = value;
//         }
//
//         public Action<IEndPoint, UnionDataList>? ServerHandler
//         {
//             set => _serverHandler = value;
//         }
//
//         /// <summary>
//         /// It is possible and acceptable for messages that are processed right now to be undelivered.
//         /// The most important invariant is to release messages.
//         /// </summary>
//         public void SetDeliverySystem(IDeliverySystem clientDeliverySystem, IDeliverySystem serverDeliverySystem)
//         {
//             if (clientDeliverySystem != _clientDeliverySystem)
//             {
//                 clientDeliverySystem.Delivered += OnClientDeliveredMessage;
//                 var oldClient = Interlocked.Exchange(ref _clientDeliverySystem, clientDeliverySystem);
//                 oldClient.Delivered -= OnClientDeliveredMessage;
//                 oldClient.Clear();
//             }
//
//             if (serverDeliverySystem != _serverDeliverySystem)
//             {
//                 serverDeliverySystem.Delivered += OnServerDeliveredMessage;
//                 var oldServer = Interlocked.Exchange(ref _serverDeliverySystem, serverDeliverySystem);
//                 oldServer.Delivered -= OnServerDeliveredMessage;
//                 oldServer.Clear();
//             }
//         }
//         
//         private void OnClientDeliveredMessage(UnionDataList message)
//         {
//             var handler = _clientHandler;
//
//             if (handler != null)
//                 handler(message);
//             else
//                 message.Release();
//         }
//
//         private void OnServerDeliveredMessage(UnionDataList message)
//         {
//             var handler = _serverHandler;
//
//             if (handler != null)
//                 handler(_clientEp, message);
//             else
//                 message.Release();
//         }
//
//         public SendResult SendToClient(UnionDataList message)
//         {
//             if (_disposed)
//             {
//                 message.Release();
//                 return SendResult.NotConnected;
//             }
//
//             _clientDeliverySystem.Deliver(message);
//             return SendResult.Ok;
//         }
//
//         public SendResult SendToServer(UnionDataList message)
//         {
//             if (_disposed)
//             {
//                 message.Release();
//                 return SendResult.NotConnected;
//             }
//
//             _serverDeliverySystem.Deliver(message);
//             return SendResult.Ok;
//         }
//
//         public void Dispose()
//         {
//             if (_disposed) return;
//             _disposed = true;
//
//             var oldClient = Interlocked.Exchange(ref _clientDeliverySystem, new PerfectDeliverySystem());
//             oldClient.Delivered -= OnClientDeliveredMessage;
//             oldClient.Clear();
//
//             var oldServer = Interlocked.Exchange(ref _serverDeliverySystem, new PerfectDeliverySystem());
//             oldServer.Delivered -= OnServerDeliveredMessage;
//             oldServer.Clear();
//
//             _clientHandler = null;
//             _serverHandler = null;
//         }
//     }
// }
