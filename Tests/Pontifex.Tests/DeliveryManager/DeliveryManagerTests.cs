using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.DeliveryManager;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager.Tests
{
    [Category("DeliveryManager")]
    public class DeliveryManagerTests
    {
        private const int MaxMsgSize = 100;
        private static IMemoryRental Memory => MemoryRental.Shared;
        private static IPool<IMultiRefByteArray, int> Pool => Memory.ByteArraysPool;
        private static ICollectablePool CPool => Memory.CollectablePool;
        private static IDeliveryAttemptScheduler RetryScheduler => new RetryDeliveryScheduler(TimeSpan.FromSeconds(10));

        private static IMultiRefByteArray Data(params byte[] bytes)
        {
            var buf = Pool.Acquire(bytes.Length);
            Buffer.BlockCopy(bytes, 0, buf.Array, buf.Offset, bytes.Length);
            return buf;
        }

        private static UnionDataList DataList(params byte[] bytes)
        {
            var data = CPool.Acquire<UnionDataList>();
            if (bytes.Length > 0)
            {
                var buf = Pool.Acquire(bytes.Length);
                Buffer.BlockCopy(bytes, 0, buf.Array, buf.Offset, bytes.Length);
                buf.AddRef();
                data.PutLast(new UnionData((IMultiRefReadOnlyByteArray)buf));
                buf.Release();
            }
            return data;
        }

        private static List<Message> Capture(DeliveryManager dm, IDeliveryAttemptScheduler? scheduler = null, DateTime? now = null)
        {
            var sent = new List<Message>();
            dm.ProcessOutgoing(scheduler ?? RetryScheduler, now ?? DateTime.UtcNow,
                new ConsumerDelegate<Message>(x => { sent.Add(x); return true; }));
            return sent;
        }

        [Test]
        public void ScheduleDelivery_SingleChunk_Ok()
        {
            var dm = new DeliveryManager(MaxMsgSize, Pool, CPool);
            Assert.That(dm.ScheduleDelivery(DataList(1, 2, 3), out _), Is.EqualTo(SendResult.Ok));
        }

        [Test]
        public void ScheduleDelivery_InvalidData_ReturnsInvalidMessage()
        {
            var dm = new DeliveryManager(MaxMsgSize, Pool, CPool);
            Assert.That(dm.ScheduleDelivery(null!, out _), Is.EqualTo(SendResult.InvalidMessage));
        }

        [Test]
        public void ScheduleDelivery_MessageTooBig_ReturnsMessageTooBig()
        {
            var dm = new DeliveryManager(MaxMsgSize, Pool, CPool);
            Assert.That(dm.ScheduleDelivery(DataList(new byte[30000]), out _), Is.EqualTo(SendResult.MessageTooBig));
        }

        [Test]
        public void ProcessOutgoing_ProducesBuffer()
        {
            var dm = new DeliveryManager(MaxMsgSize, Pool, CPool);
            dm.ScheduleDelivery(DataList(10, 20, 30), out _);
            var sent = Capture(dm);
            Assert.That(sent, Has.Count.EqualTo(1));
            foreach (var m in sent) m.Data.Release();
        }

        [Test]
        public void RoundTrip_SingleChunk_ReceivedFires()
        {
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var receiver = new DeliveryManager(MaxMsgSize, Pool, CPool);

            sender.ScheduleDelivery(DataList(1, 2, 3, 4, 5), out _);
            var outbound = Capture(sender);
            Assert.That(outbound, Has.Count.EqualTo(1));

            var msg = outbound[0];
            msg.Data.AddRef();

            DeliveryId? receivedId = null;
            byte[]? receivedBytes = null;
            receiver.Received += (id, d, _) =>
            {
                receivedId = id;
                var element = d.Elements[0].Bytes!;
                receivedBytes = new byte[element.Count];
                element.CopyTo(receivedBytes, 0, 0, element.Count);
            };

            receiver.ProcessIncoming(new Message(msg.PacketId, msg.Data));
            msg.Data.Release();

            Assert.That(receivedId, Is.EqualTo(new DeliveryId(1)));
            Assert.That(receivedBytes, Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));
        }

        [Test]
        public void DuplicateInbound_ReceivedFiresOnce()
        {
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var receiver = new DeliveryManager(MaxMsgSize, Pool, CPool);

            sender.ScheduleDelivery(DataList(1, 2, 3), out _);
            var outbound = Capture(sender);
            var msg = outbound[0];

            int received = 0;
            receiver.Received += (_, _, _) => received++;

            msg.Data.AddRef();
            receiver.ProcessIncoming(new Message(msg.PacketId, msg.Data));
            msg.Data.AddRef();
            receiver.ProcessIncoming(new Message(msg.PacketId, msg.Data));
            msg.Data.Release();

            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void AckRoundTrip_DeliveredFires()
        {
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var receiver = new DeliveryManager(MaxMsgSize, Pool, CPool);

            DeliveryId? deliveredId = null;
            sender.Delivered += id => deliveredId = id;

            sender.ScheduleDelivery(DataList(1, 2, 3), out var deliveryId);
            var toReceiver = Capture(sender);
            Assert.That(toReceiver, Has.Count.EqualTo(1));

            var msg = toReceiver[0];
            msg.Data.AddRef();
            receiver.ProcessIncoming(new Message(msg.PacketId, msg.Data));
            msg.Data.Release();

            var toSender = Capture(receiver);
            Assert.That(toSender, Has.Count.GreaterThanOrEqualTo(1));

            foreach (var ack in toSender)
            {
                ack.Data.AddRef();
                sender.ProcessIncoming(new Message(ack.PacketId, ack.Data));
                ack.Data.Release();
            }

            Assert.That(deliveredId, Is.EqualTo(deliveryId));
        }

        [Test]
        public void DeliveryFailure_FiresFailedToDeliver()
        {
            var dm = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var now = DateTime.UtcNow;
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromMilliseconds(50), baseIntervalMs: 100);

            DeliveryId? failedId = null;
            dm.FailedToDeliver += id => failedId = id;

            dm.ScheduleDelivery(DataList(1), out var dmFailedId);
            var batch1 = Capture(dm, scheduler, now);
            foreach (var m in batch1) m.Data.Release();

            var batch2 = Capture(dm, scheduler, now + TimeSpan.FromMilliseconds(200));
            foreach (var m in batch2) m.Data.Release();

            Assert.That(failedId, Is.EqualTo(dmFailedId));
        }

        [Test]
        public void Clear_ThenProcessOutgoing_ProducesNothing()
        {
            var dm = new DeliveryManager(MaxMsgSize, Pool, CPool);
            dm.ScheduleDelivery(DataList(1, 2, 3), out _);
            dm.Clear();
            Assert.That(Capture(dm), Is.Empty);
        }

        [Test]
        public void MultipleSends_AllReceived()
        {
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var receiver = new DeliveryManager(MaxMsgSize, Pool, CPool);

            for (ushort i = 1; i <= 5; i++)
                sender.ScheduleDelivery(DataList((byte)i), out _);

            var outbound = Capture(sender);
            Assert.That(outbound, Has.Count.EqualTo(5));

            int received = 0;
            receiver.Received += (_, _, _) => received++;

            foreach (var msg in outbound)
            {
                msg.Data.AddRef();
                receiver.ProcessIncoming(new Message(msg.PacketId, msg.Data));
                msg.Data.Release();
            }

            Assert.That(received, Is.EqualTo(5));
        }

        [Test]
        public void GarbageBytes_DoesNotCrash()
        {
            var dm = new DeliveryManager(MaxMsgSize, Pool, CPool);
            bool result = dm.ProcessIncoming(new Message(1, CreateGarbageList()));
            Assert.That(result, Is.False);
        }

        private static UnionDataList CreateGarbageList()
        {
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)0xFF));
            return data;
        }

        [Test]
        public void EmptyDataRoundTrip_Works()
        {
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var receiver = new DeliveryManager(MaxMsgSize, Pool, CPool);

            sender.ScheduleDelivery(DataList(), out _);
            var outbound = Capture(sender);
            var msg = outbound[0];
            msg.Data.AddRef();

            byte[]? receivedBytes = null;
            receiver.Received += (_, d, _) =>
            {
                if (d.Elements.Count > 0)
                {
                    var element = d.Elements[0].Bytes!;
                    receivedBytes = new byte[element.Count];
                    element.CopyTo(receivedBytes, 0, 0, element.Count);
                }
                else
                {
                    receivedBytes = Array.Empty<byte>();
                }
            };

            receiver.ProcessIncoming(new Message(msg.PacketId, msg.Data));
            msg.Data.Release();

            Assert.That(receivedBytes, Is.Not.Null);
            Assert.That(receivedBytes!.Length, Is.EqualTo(0));
        }

        [Test]
        public void ProcessOutgoing_WithoutSchedule_ProducesNothing()
        {
            Assert.That(Capture(new DeliveryManager(MaxMsgSize, Pool, CPool)), Is.Empty);
        }

        [Test]
        public void MultipleProcessOutgoing_ConfirmationsAccumulate()
        {
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var receiver = new DeliveryManager(MaxMsgSize, Pool, CPool);

            sender.ScheduleDelivery(DataList(1, 2, 3), out _);
            var batch1 = Capture(sender);
            Assert.That(batch1, Has.Count.EqualTo(1));
            var msg = batch1[0];

            msg.Data.AddRef();
            receiver.ProcessIncoming(new Message(msg.PacketId, msg.Data));
            msg.Data.Release();

            var ack1 = Capture(receiver);
            Assert.That(ack1, Has.Count.EqualTo(1));
            foreach (var a in ack1) a.Data.Release();

            Assert.That(Capture(receiver), Is.Empty);
        }

        [Test]
        public void MultiChunkRoundTrip_WithLargeData()
        {
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var receiver = new DeliveryManager(MaxMsgSize, Pool, CPool);

            var bytes = new byte[150];
            for (int i = 0; i < 150; i++) bytes[i] = (byte)(i % 256);
            var largeData = DataList(bytes);

            sender.ScheduleDelivery(largeData, out _);
            var outbound = Capture(sender);
            Assert.That(outbound.Count, Is.GreaterThan(1));

            byte[]? resultData = null;
            receiver.Received += (_, d, _) =>
            {
                if (d.Elements.Count > 0)
                {
                    var element = d.Elements[0].Bytes!;
                    resultData = new byte[element.Count];
                    element.CopyTo(resultData, 0, 0, element.Count);
                }
            };

            foreach (var msg in outbound)
            {
                msg.Data.AddRef();
                receiver.ProcessIncoming(new Message(msg.PacketId, msg.Data));
                msg.Data.Release();
            }

            Assert.That(resultData, Is.Not.Null);
            Assert.That(resultData!.Length, Is.EqualTo(150));
            for (int i = 0; i < 150; i++)
                Assert.That(resultData[i], Is.EqualTo((byte)(i % 256)));
        }

        [Test]
        public void MultiChunkRoundTrip_OutOfOrder()
        {
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var receiver = new DeliveryManager(MaxMsgSize, Pool, CPool);

            var bytes = new byte[150];
            for (int i = 0; i < 150; i++) bytes[i] = (byte)(i % 256);
            var largeData = DataList(bytes);

            sender.ScheduleDelivery(largeData, out _);
            var outbound = Capture(sender);
            Assert.That(outbound.Count, Is.GreaterThan(1));

            byte[]? resultData = null;
            receiver.Received += (_, d, _) =>
            {
                if (d.Elements.Count > 0)
                {
                    var element = d.Elements[0].Bytes!;
                    resultData = new byte[element.Count];
                    element.CopyTo(resultData, 0, 0, element.Count);
                }
            };

            for (int i = outbound.Count - 1; i >= 0; i--)
            {
                var msg = outbound[i];
                msg.Data.AddRef();
                receiver.ProcessIncoming(new Message(msg.PacketId, msg.Data));
                msg.Data.Release();
            }

            Assert.That(resultData, Is.Not.Null);
            Assert.That(resultData!.Length, Is.EqualTo(150));
            for (int i = 0; i < 150; i++)
                Assert.That(resultData[i], Is.EqualTo((byte)(i % 256)));
        }

        [Test]
        public void MultiChunk_DuplicateChunk_ReceivedFiresOnce()
        {
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var receiver = new DeliveryManager(MaxMsgSize, Pool, CPool);

            var bytes = new byte[150];
            for (int i = 0; i < 150; i++) bytes[i] = (byte)(i % 256);
            var largeData = DataList(bytes);

            sender.ScheduleDelivery(largeData, out _);
            var outbound = Capture(sender);
            Assert.That(outbound.Count, Is.GreaterThan(1));

            int received = 0;
            receiver.Received += (_, _, _) => received++;

            foreach (var msg in outbound)
            {
                msg.Data.AddRef();
                receiver.ProcessIncoming(new Message(msg.PacketId, msg.Data));
                msg.Data.Release();
            }

            var first = outbound[0];
            first.Data.AddRef();
            receiver.ProcessIncoming(new Message(first.PacketId, first.Data));
            first.Data.Release();

            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void MultiChunkAckRoundTrip_DeliveredFires()
        {
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var receiver = new DeliveryManager(MaxMsgSize, Pool, CPool);

            DeliveryId? deliveredId = null;
            sender.Delivered += id => deliveredId = id;

            var bytes = new byte[150];
            for (int i = 0; i < 150; i++) bytes[i] = (byte)(i % 256);
            var largeData = DataList(bytes);

            sender.ScheduleDelivery(largeData, out _);
            var toReceiver = Capture(sender);
            Assert.That(toReceiver.Count, Is.GreaterThan(1));

            foreach (var msg in toReceiver)
            {
                msg.Data.AddRef();
                receiver.ProcessIncoming(new Message(msg.PacketId, msg.Data));
                msg.Data.Release();
            }

            var toSender = Capture(receiver);
            Assert.That(toSender.Count, Is.GreaterThanOrEqualTo(1));

            foreach (var ack in toSender)
            {
                ack.Data.AddRef();
                sender.ProcessIncoming(new Message(ack.PacketId, ack.Data));
                ack.Data.Release();
            }

            Assert.That(deliveredId, Is.EqualTo(new DeliveryId(1)));
        }

        [Test]
        public void MultiChunkRetryTimeout_FiresFailedToDeliver()
        {
            var dm = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var now = DateTime.UtcNow;
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromMilliseconds(50), baseIntervalMs: 100);

            DeliveryId? failedId = null;
            dm.FailedToDeliver += id => failedId = id;

            var bytes = new byte[150];
            var largeData = DataList(bytes);

            dm.ScheduleDelivery(largeData, out _);
            var batch1 = Capture(dm, scheduler, now);
            Assert.That(batch1.Count, Is.GreaterThan(1));
            foreach (var m in batch1) m.Data.Release();

            var batch2 = Capture(dm, scheduler, now + TimeSpan.FromMilliseconds(200));
            foreach (var m in batch2) m.Data.Release();

            Assert.That(failedId, Is.EqualTo(new DeliveryId(1)));
        }
    }
}
