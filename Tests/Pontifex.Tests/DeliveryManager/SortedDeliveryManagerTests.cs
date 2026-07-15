using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.DeliveryManager;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager.Tests
{
    [Category("DeliveryManager")]
    public class SortedDeliveryManagerTests
    {
        private const int MaxMsgSize = 100;
        private static IMemoryRental Memory => MemoryRental.Shared;
        private static IPool<IMultiRefByteArray, int> Pool => Memory.ByteArraysPool;
        private static ICollectablePool CPool => Memory.CollectablePool;
        private static IDeliveryAttemptScheduler Scheduler => new RetryDeliveryScheduler(TimeSpan.FromSeconds(10));

        private static IMultiRefByteArray Data(params byte[] bytes)
        {
            var buf = Pool.Acquire(bytes.Length);
            Buffer.BlockCopy(bytes, 0, buf.Array, buf.Offset, bytes.Length);
            return buf;
        }

        private static List<Message> CaptureAll(DeliveryManager dm)
        {
            var sent = new List<Message>();
            dm.ProcessOutgoing(Scheduler, DateTime.UtcNow,
                new ConsumerDelegate<Message>(x => { sent.Add(x); return true; }));
            return sent;
        }

        [Test]
        public void AheadIdsBuffered_ReceivedInOrder()
        {
            var inner = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var sorted = new SortedDeliveryManager(inner);
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);

            sender.ScheduleDelivery(new DeliveryId(1), Data(1));
            sender.ScheduleDelivery(new DeliveryId(3), Data(3));
            sender.ScheduleDelivery(new DeliveryId(2), Data(2));
            var outbound = CaptureAll(sender);
            Assert.That(outbound, Has.Count.EqualTo(3));

            var received = new List<(DeliveryId id, byte[] data)>();
            sorted.Received += (id, d, _) =>
            {
                var bytes = new byte[d.Count];
                d.CopyTo(bytes, 0, 0, d.Count);
                received.Add((id, bytes));
            };

            foreach (var msg in outbound)
            {
                msg.Data.AddRef();
                inner.ProcessIncoming(new Message(msg.PacketId, msg.Data));
                msg.Data.Release();
            }

            Assert.That(received.Count, Is.EqualTo(3));
            Assert.That(received[0].id, Is.EqualTo(new DeliveryId(1)));
            Assert.That(received[1].id, Is.EqualTo(new DeliveryId(2)));
            Assert.That(received[2].id, Is.EqualTo(new DeliveryId(3)));
        }

        [Test]
        public void SingleMessage_PassesThrough()
        {
            var inner = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var sorted = new SortedDeliveryManager(inner);
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);

            sender.ScheduleDelivery(new DeliveryId(5), Data(42));
            var outbound = CaptureAll(sender);

            byte[]? received = null;
            sorted.Received += (_, d, _) =>
            {
                received = new byte[d.Count];
                d.CopyTo(received, 0, 0, d.Count);
            };

            var msg = outbound[0];
            msg.Data.AddRef();
            inner.ProcessIncoming(new Message(msg.PacketId, msg.Data));
            msg.Data.Release();

            Assert.That(received, Is.Not.Null);
            Assert.That(received![0], Is.EqualTo(42));
        }

        [Test]
        public void Clear_StopsFurtherProcessing()
        {
            var inner = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var sorted = new SortedDeliveryManager(inner);
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);

            sender.ScheduleDelivery(new DeliveryId(1), Data(1));
            var outbound = CaptureAll(sender);

            sorted.Clear();

            int receivedCount = 0;
            sorted.Received += (_, _, _) => receivedCount++;

            foreach (var msg in outbound)
            {
                msg.Data.AddRef();
                inner.ProcessIncoming(new Message(msg.PacketId, msg.Data));
                msg.Data.Release();
            }

            Assert.That(receivedCount, Is.EqualTo(0));
        }

        [Test]
        public void DuplicateMessage_NotSentToSorter()
        {
            var inner = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var sorted = new SortedDeliveryManager(inner);
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);

            sender.ScheduleDelivery(new DeliveryId(1), Data(1));
            var outbound = CaptureAll(sender);
            var msg = outbound[0];

            int received = 0;
            sorted.Received += (_, _, _) => received++;

            msg.Data.AddRef();
            inner.ProcessIncoming(new Message(msg.PacketId, msg.Data));
            msg.Data.AddRef();
            inner.ProcessIncoming(new Message(msg.PacketId, msg.Data));
            msg.Data.Release();

            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void BusyOrder_MultipleBatches()
        {
            var inner = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var sorted = new SortedDeliveryManager(inner);
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);

            for (ushort i = 1; i <= 10; i++)
                sender.ScheduleDelivery(new DeliveryId(i), Data((byte)i));
            var outbound = CaptureAll(sender);
            Assert.That(outbound, Has.Count.EqualTo(10));

            var received = new List<DeliveryId>();
            sorted.Received += (id, _, _) => received.Add(id);

            foreach (var msg in outbound)
            {
                msg.Data.AddRef();
                inner.ProcessIncoming(new Message(msg.PacketId, msg.Data));
                msg.Data.Release();
            }

            Assert.That(received.Count, Is.EqualTo(10));
            for (int i = 0; i < 10; i++)
                Assert.That(received[i].Id, Is.EqualTo(i + 1));
        }
    }
}
