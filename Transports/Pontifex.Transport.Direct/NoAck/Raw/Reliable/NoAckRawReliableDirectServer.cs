// using System;
// using Actuarius.Memory;
// using Pontifex.NoAck.Raw.Direct;
// using Pontifex.Utils;
// using Scriba;
//
// namespace Pontifex.NoAck.Raw.Reliable.Direct
// {
//     public sealed class NoAckRawReliableDirectServer : NoAckRawDirectServer, INoAckRawReliableServer
//     {
//         public override TransportType Type => TransportType.NoAckRawReliable;
//
//         public NoAckRawReliableDirectServer(string serverName, ILogger logger, IMemoryRental memoryRental)
//             : base(serverName, "direct-noack-raw-reliable", logger, memoryRental)
//         {
//         }
//
//         public SendResult Send(IEndPoint destination, UnionDataList message) => SendToClient(destination, message);
//     }
// }
