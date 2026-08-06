using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Delivery;
using Pontifex.Utils;

namespace Pontifex.Delivery.Tests
{
    [Category("DeliveryManager")]
    public class DeliverySortingManagerTests
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

        private static List<UnionDataList> CaptureAll(DeliveryManager dm, DateTime? now = null)
        {
            var sent = new List<UnionDataList>();
            dm.ProcessOutgoing(Scheduler, now ?? DateTime.UtcNow,
                new ConsumerDelegate<UnionDataList>(x => { sent.Add(x); return true; }));
            return sent;
        }

        [Test]
        public void AheadIdsBuffered_ReceivedInOrder()
        {
            var inner = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var sorted = new DeliverySortingManager(inner);
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);

            sender.ScheduleDelivery(DataList(1), out _);
            sender.ScheduleDelivery(DataList(3), out _);
            sender.ScheduleDelivery(DataList(2), out _);
            var outbound = CaptureAll(sender);
            Assert.That(outbound, Has.Count.EqualTo(3));

            var received = new List<(DeliveryId id, byte[] data)>();
            sorted.Received += (id, d) =>
            {
                var element = d.Elements[0].Bytes!;
                var bytes = new byte[element.Count];
                element.CopyTo(bytes, 0, 0, element.Count);
                received.Add((id, bytes));
            };

            foreach (var msg in outbound)
            {
                msg.AddRef();
                inner.ProcessIncoming(msg);
                msg.Release();
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
            var sorted = new DeliverySortingManager(inner);
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);

            sender.ScheduleDelivery(DataList(42), out _);
            var outbound = CaptureAll(sender);

            byte[]? received = null;
            sorted.Received += (_, d) =>
            {
                var element = d.Elements[0].Bytes!;
                received = new byte[element.Count];
                element.CopyTo(received, 0, 0, element.Count);
            };

            var msg = outbound[0];
            msg.AddRef();
            inner.ProcessIncoming(msg);
            msg.Release();

            Assert.That(received, Is.Not.Null);
            Assert.That(received![0], Is.EqualTo(42));
        }

        [Test]
        public void Clear_StopsFurtherProcessing()
        {
            var inner = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var sorted = new DeliverySortingManager(inner);
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);

            sender.ScheduleDelivery(DataList(1), out _);
            var outbound = CaptureAll(sender);

            sorted.Clear();

            int receivedCount = 0;
            sorted.Received += (_, _) => receivedCount++;

            foreach (var msg in outbound)
            {
                msg.AddRef();
                inner.ProcessIncoming(msg);
                msg.Release();
            }

            Assert.That(receivedCount, Is.EqualTo(0));
        }

        [Test]
        public void DuplicateMessage_NotSentToSorter()
        {
            var inner = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var sorted = new DeliverySortingManager(inner);
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);

            sender.ScheduleDelivery(DataList(1), out _);
            var outbound = CaptureAll(sender);
            var msg = outbound[0];

            int received = 0;
            sorted.Received += (_, _) => received++;

            msg.AddRef();
            inner.ProcessIncoming(msg);
            msg.AddRef();
            inner.ProcessIncoming(msg);
            msg.Release();

            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void BusyOrder_MultipleBatches()
        {
            var inner = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var sorted = new DeliverySortingManager(inner);
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);

            for (ushort i = 1; i <= 10; i++)
                sender.ScheduleDelivery(DataList((byte)i), out _);
            var outbound = CaptureAll(sender);
            Assert.That(outbound, Has.Count.EqualTo(10));

            var received = new List<DeliveryId>();
            sorted.Received += (id, _) => received.Add(id);

            foreach (var msg in outbound)
            {
                msg.AddRef();
                inner.ProcessIncoming(msg);
                msg.Release();
            }

            Assert.That(received.Count, Is.EqualTo(10));
            for (int i = 0; i < 10; i++)
                Assert.That(received[i].Id, Is.EqualTo(i + 1));
        }

        [Test]
        public void GapInSequence_FiresFailedToSort()
        {
            // Transport reordering causes non-sequential DeliveryId delivery:
            // packet 2 arrives before packet 1 → sorter advances past DeliveryId(1)
            var inner = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var sorted = new DeliverySortingManager(inner);
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);

            var t0 = DateTime.UtcNow;

            sender.ScheduleDelivery(DataList(1), out _); // DeliveryId=1
            var msg1batch = CaptureAll(sender, t0);
            Assert.That(msg1batch, Has.Count.EqualTo(1));

            sender.ScheduleDelivery(DataList(2), out _); // DeliveryId=2
            var msg2batch = CaptureAll(sender, t0 + TimeSpan.FromMilliseconds(50));
            Assert.That(msg2batch, Has.Count.EqualTo(1));

            bool failed = false;
            sorted.FailedToSort += () => failed = true;

            // deliver packet 2 first (reordered) — guaranteed by separate captures
            var msg2 = msg2batch[0];
            msg2.AddRef();
            inner.ProcessIncoming(msg2);
            msg2.Release();

            // now deliver packet 1 — sorter._id advanced to 3, Push(1) fails
            var msg1 = msg1batch[0];
            msg1.AddRef();
            inner.ProcessIncoming(msg1);
            msg1.Release();

            Assert.That(failed, Is.True);
        }

        [Test]
        public void AfterGap_SorterStillDeliversLaterMessages()
        {
            // Push failure (id < _id) fires FailedToSort but does NOT kill the sorter.
            // Future messages with higher IDs are still accepted and delivered.
            var inner = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var sorted = new DeliverySortingManager(inner);
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);

            var t0 = DateTime.UtcNow;

            sender.ScheduleDelivery(DataList(1), out _); // DeliveryId=1
            var msg1batch = CaptureAll(sender, t0);
            Assert.That(msg1batch, Has.Count.EqualTo(1));

            sender.ScheduleDelivery(DataList(2), out _); // DeliveryId=2
            var msg2batch = CaptureAll(sender, t0 + TimeSpan.FromMilliseconds(50));
            Assert.That(msg2batch, Has.Count.EqualTo(1));

            // deliver packet 2 first — sorter advances past DeliveryId=1
            var msg2 = msg2batch[0];
            msg2.AddRef();
            inner.ProcessIncoming(msg2);
            msg2.Release();

            // deliver packet 1 — Push(1) fails because 1 < _id(3), fires FailedToSort
            var msg1 = msg1batch[0];
            msg1.AddRef();
            inner.ProcessIncoming(msg1);
            msg1.Release();

            // sorter is NOT dead — send a new message with higher DeliveryId
            sender.ScheduleDelivery(DataList(3), out _); // DeliveryId=3
            var third = CaptureAll(sender);

            int receivedAfterGap = 0;
            sorted.Received += (_, _) => receivedAfterGap++;

            foreach (var msg in third)
            {
                msg.AddRef();
                inner.ProcessIncoming(msg);
                msg.Release();
            }

            Assert.That(receivedAfterGap, Is.EqualTo(1));
        }

        [Test]
        public void AfterClear_FailedToSortFiresOnNextMessage()
        {
            var inner = new DeliveryManager(MaxMsgSize, Pool, CPool);
            var sorted = new DeliverySortingManager(inner);
            var sender = new DeliveryManager(MaxMsgSize, Pool, CPool);

            // send two messages so the dedup window has entries
            sender.ScheduleDelivery(DataList(1), out _); // PacketId=1
            sender.ScheduleDelivery(DataList(2), out _); // PacketId=2
            var outbound = CaptureAll(sender);
            // process both so dedup window covers 1..2
            foreach (var msg in outbound)
            {
                msg.AddRef();
                inner.ProcessIncoming(msg);
                msg.Release();
            }

            sorted.Clear();

            bool failed = false;
            sorted.FailedToSort += () => failed = true;

            // send a third message with fresh PacketId=3 → passes Deduplicator as New
            sender.ScheduleDelivery(DataList(3), out _);
            var third = CaptureAll(sender);
            foreach (var msg in third)
            {
                msg.AddRef();
                inner.ProcessIncoming(msg);
                msg.Release();
            }

            Assert.That(failed, Is.True);
        }
    }
}
