using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.DeliveryManager;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager.Tests
{
    [Category("DeliveryManager")]
    public class DeliveryDispatcherTests
    {
        private static IMemoryRental Memory => MemoryRental.Shared;
        private static IPool<IMultiRefByteArray, int> Pool => Memory.ByteArraysPool;
        private static ICollectablePool CPool => Memory.CollectablePool;

        private static UnionDataList DummyData()
        {
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)0));
            return data;
        }

        private static DeliveryInfo Info(ushort id, byte chunk = 0) => new DeliveryInfo(new DeliveryId(id), chunk);

        [Test]
        public void ScheduleDeliver_ReturnsOk()
        {
            var d = new DeliveryDispatcher(10);
            Assert.That(d.ScheduleDeliver(Info(1), DummyData(), DateTime.UtcNow),
                Is.EqualTo(DeliveryDispatcher.ScheduleResult.Ok));
        }

        [Test]
        public void ScheduleDeliver_DuplicateId_ReturnsIdIsNotUnique()
        {
            var d = new DeliveryDispatcher(10);
            d.ScheduleDeliver(Info(1), DummyData(), DateTime.UtcNow);
            var result = d.ScheduleDeliver(Info(1), DummyData(), DateTime.UtcNow);
            Assert.That(result, Is.EqualTo(DeliveryDispatcher.ScheduleResult.IdIsNotUnique));
        }

        [Test]
        public void ScheduleDeliver_AtCapacity_ReturnsBufferOverflow()
        {
            var d = new DeliveryDispatcher(2);
            d.ScheduleDeliver(Info(1), DummyData(), DateTime.UtcNow);
            d.ScheduleDeliver(Info(2), DummyData(), DateTime.UtcNow);
            var result = d.ScheduleDeliver(Info(3), DummyData(), DateTime.UtcNow);
            Assert.That(result, Is.EqualTo(DeliveryDispatcher.ScheduleResult.BufferOverflow));
        }

        [Test]
        public void TryToDeliver_NoDueTasks_SendsNothing()
        {
            var d = new DeliveryDispatcher(10);
            d.ScheduleDeliver(Info(1), DummyData(), DateTime.UtcNow);

            var sent = new List<Message>();
            var consumer = new ConsumerDelegate<Message>(x => { sent.Add(x); return true; });
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromSeconds(10));

            d.TryToDeliver(consumer, scheduler, DateTime.UtcNow - TimeSpan.FromHours(1));

            Assert.That(sent, Is.Empty);
        }

        [Test]
        public void TryToDeliver_DueTask_SendsOnce()
        {
            var d = new DeliveryDispatcher(10);
            var now = DateTime.UtcNow;
            d.ScheduleDeliver(Info(1), DummyData(), now);

            var sent = new List<Message>();
            var consumer = new ConsumerDelegate<Message>(x => { sent.Add(x); return true; });
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromSeconds(10));

            d.TryToDeliver(consumer, scheduler, now + TimeSpan.FromMilliseconds(1));

            Assert.That(sent, Has.Count.EqualTo(1));
            Assert.That(sent[0].PacketId, Is.EqualTo(1));
            sent[0].Data.Release();
        }

        [Test]
        public void TryToDeliver_ConfirmedDelivery_Skipped()
        {
            var d = new DeliveryDispatcher(10);
            var now = DateTime.UtcNow;
            d.ScheduleDeliver(Info(1), DummyData(), now);
            d.ConfirmDelivered(Info(1));

            var sent = new List<Message>();
            var consumer = new ConsumerDelegate<Message>(x => { sent.Add(x); return true; });
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromSeconds(10));

            d.TryToDeliver(consumer, scheduler, now + TimeSpan.FromMilliseconds(1));

            Assert.That(sent, Is.Empty);
        }

        [Test]
        public void TryToDeliver_Retry_SendsMultipleTimes()
        {
            var d = new DeliveryDispatcher(10);
            var now = DateTime.UtcNow;
            d.ScheduleDeliver(Info(1), DummyData(), now);

            var sent = new List<Message>();
            var consumer = new ConsumerDelegate<Message>(x => { sent.Add(x); return true; });
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromSeconds(10), baseIntervalMs: 50);

            d.TryToDeliver(consumer, scheduler, now);
            d.TryToDeliver(consumer, scheduler, now + TimeSpan.FromMilliseconds(100));
            d.TryToDeliver(consumer, scheduler, now + TimeSpan.FromMilliseconds(200));

            Assert.That(sent, Has.Count.EqualTo(3));
            foreach (var b in sent) b.Data.Release();
        }

        [Test]
        public void TryToDeliver_Failure_FiresFailedToDeliver()
        {
            var d = new DeliveryDispatcher(10);
            var now = DateTime.UtcNow;
            d.ScheduleDeliver(Info(1), DummyData(), now);

            DeliveryId? failedId = null;
            d.OnFailedToDeliver += id => failedId = id;

            var sent = new List<Message>();
            var consumer = new ConsumerDelegate<Message>(x => { sent.Add(x); return true; });
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromMilliseconds(50), baseIntervalMs: 100);

            d.TryToDeliver(consumer, scheduler, now);
            d.TryToDeliver(consumer, scheduler, now + TimeSpan.FromMilliseconds(200));

            Assert.That(failedId, Is.EqualTo(new DeliveryId(1)));
            foreach (var b in sent) b.Data.Release();
        }

        [Test]
        public void ConfirmDelivered_SingleChunk_FiresDelivered()
        {
            var d = new DeliveryDispatcher(10);
            var now = DateTime.UtcNow;
            d.ScheduleDeliver(Info(1), DummyData(), now);

            DeliveryId? deliveredId = null;
            d.OnDelivered += id => deliveredId = id;

            d.ConfirmDelivered(Info(1));
            Assert.That(deliveredId, Is.EqualTo(new DeliveryId(1)));
        }

        [Test]
        public void ConfirmDelivered_MultiChunk_AllConfirmed_FiresOnce()
        {
            var d = new DeliveryDispatcher(10);
            var now = DateTime.UtcNow;
            d.ScheduleDeliver(Info(1, 0), DummyData(), now);
            d.ScheduleDeliver(Info(1, 1), DummyData(), now);

            int delivered = 0;
            d.OnDelivered += _ => delivered++;

            d.ConfirmDelivered(Info(1, 0));
            Assert.That(delivered, Is.EqualTo(0));

            d.ConfirmDelivered(Info(1, 1));
            Assert.That(delivered, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmDelivered_NonExistent_NoOp()
        {
            var d = new DeliveryDispatcher(10);
            bool fired = false;
            d.OnDelivered += _ => fired = true;
            d.ConfirmDelivered(Info(999));
            Assert.That(fired, Is.False);
        }

        [Test]
        public void Clear_EmptiesQueue()
        {
            var d = new DeliveryDispatcher(10);
            var now = DateTime.UtcNow;
            d.ScheduleDeliver(Info(1), DummyData(), now);
            d.ScheduleDeliver(Info(2), DummyData(), now);
            d.Clear();

            var sent = new List<Message>();
            var consumer = new ConsumerDelegate<Message>(x => { sent.Add(x); return true; });
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromSeconds(10));

            d.TryToDeliver(consumer, scheduler, now + TimeSpan.FromHours(1));

            Assert.That(sent, Is.Empty);
        }

        [Test]
        public void MultipleTasks_AllDue_AllSent()
        {
            var d = new DeliveryDispatcher(10);
            var now = DateTime.UtcNow;
            d.ScheduleDeliver(Info(1), DummyData(), now);
            d.ScheduleDeliver(Info(2), DummyData(), now);
            d.ScheduleDeliver(Info(3), DummyData(), now);

            var sent = new List<Message>();
            var consumer = new ConsumerDelegate<Message>(x => { sent.Add(x); return true; });
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromSeconds(10));

            d.TryToDeliver(consumer, scheduler, now + TimeSpan.FromMilliseconds(1));

            Assert.That(sent, Has.Count.EqualTo(3));
            foreach (var b in sent) b.Data.Release();
        }
    }
}
