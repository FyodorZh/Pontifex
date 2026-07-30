// using System;
// using Actuarius.Memory;
// using Pontifex.NoAck.Raw.Direct;
// using Pontifex.Utils;
// using Pontifex.VirtualDelivery;
// using Scriba;
//
// namespace Pontifex.NoAck.Raw.Reliable.Direct
// {
//     public sealed class NoAckRawReliableDirectClient : NoAckRawDirectClient, INoAckRawReliableClient
//     {
//         public override TransportType Type => TransportType.NoAckRawReliable;
//
//         public NoAckRawReliableDirectClient(string serverName, ILogger logger, IMemoryRental memoryRental)
//             : base(serverName, "direct-noack-raw-reliable", logger, memoryRental)
//         {
//         }
//
//         protected override void OnChannelConnected(Channel channel)
//         {
//             base.OnChannelConnected(channel);
//             channel.SetDeliverySystem(new PerfectDeliverySystem(), new PerfectDeliverySystem());
//         }
//
//         protected override void OnBeforeChannelDisconnect(Channel channel)
//         {
//             channel.SetDeliverySystem(new PerfectDeliverySystem(), new PerfectDeliverySystem());
//         }
//
//         public SendResult Send(UnionDataList message) => SendToServer(message);
//     }
// }
