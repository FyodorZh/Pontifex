using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.Delivery.Tests
{
    [Category("DeliveryManager")]
    public class DeliveryInfoSerializerTests
    {
        private static IMemoryRental Memory => MemoryRental.Shared;
        private static IPool<IMultiRefByteArray, int> Pool => Memory.ByteArraysPool;
        private static ICollectablePool CPool => Memory.CollectablePool;

        private static DeliveryInfoSerializer CreateSerializer() => new DeliveryInfoSerializer(CPool);

        // ── CreateDeliveryReport tests ──

        [Test]
        public void CreateDeliveryReport_SingleConfirmation()
        {
            var ser = CreateSerializer();
            var confirmations = new List<DeliveryInfo> { new DeliveryInfo(new DeliveryId(42), 7) };
            var msg = ser.CreateDeliveryReport(confirmations, 0, 1);
            Assert.That(msg.Elements.Count, Is.EqualTo(3)); // count + id + chunkId
            Assert.That(msg.Elements[0].Alias.UShortValue, Is.EqualTo(1));       // count
            Assert.That(msg.Elements[1].Alias.UShortValue, Is.EqualTo(42));      // id
            Assert.That(msg.Elements[2].Alias.ByteValue, Is.EqualTo(7));         // chunkId
            msg.Release();
        }

        [Test]
        public void CreateDeliveryReport_MultipleConfirmations()
        {
            var ser = CreateSerializer();
            var confirmations = new List<DeliveryInfo>
            {
                new DeliveryInfo(new DeliveryId(1), 0),
                new DeliveryInfo(new DeliveryId(2), 1),
                new DeliveryInfo(new DeliveryId(3), 2)
            };
            var msg = ser.CreateDeliveryReport(confirmations, 0, 3);
            Assert.That(msg.Elements.Count, Is.EqualTo(1 + 2 * 3)); // count + pairs
            Assert.That(msg.Elements[0].Alias.UShortValue, Is.EqualTo(3));       // count

            Assert.That(msg.Elements[1].Alias.UShortValue, Is.EqualTo(1));
            Assert.That(msg.Elements[2].Alias.ByteValue, Is.EqualTo(0));
            Assert.That(msg.Elements[3].Alias.UShortValue, Is.EqualTo(2));
            Assert.That(msg.Elements[4].Alias.ByteValue, Is.EqualTo(1));
            Assert.That(msg.Elements[5].Alias.UShortValue, Is.EqualTo(3));
            Assert.That(msg.Elements[6].Alias.ByteValue, Is.EqualTo(2));
            msg.Release();
        }

        [Test]
        public void CreateDeliveryReport_PartialRange()
        {
            var ser = CreateSerializer();
            var confirmations = new List<DeliveryInfo>
            {
                new DeliveryInfo(new DeliveryId(10), 0),
                new DeliveryInfo(new DeliveryId(20), 1),
                new DeliveryInfo(new DeliveryId(30), 2)
            };
            var msg = ser.CreateDeliveryReport(confirmations, 1, 2);
            Assert.That(msg.Elements[0].Alias.UShortValue, Is.EqualTo(2));       // count
            Assert.That(msg.Elements[1].Alias.UShortValue, Is.EqualTo(20));
            Assert.That(msg.Elements[3].Alias.UShortValue, Is.EqualTo(30));
            msg.Release();
        }

        [Test]
        public void CreateDeliveryReport_ZeroCount()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateDeliveryReport(new List<DeliveryInfo>(), 0, 0);
            Assert.That(msg.Elements.Count, Is.EqualTo(1)); // count only
            Assert.That(msg.Elements[0].Alias.UShortValue, Is.EqualTo(0));
            msg.Release();
        }

        // ── LoadDeliveryReport tests ──

        [Test]
        public void LoadDeliveryReport_Valid_ReturnsTrueAndPopulatesList()
        {
            var ser = CreateSerializer();
            var confirmations = new List<DeliveryInfo>
            {
                new DeliveryInfo(new DeliveryId(7), 3)
            };
            var msg = ser.CreateDeliveryReport(confirmations, 0, 1);
            msg.AddRef();

            bool loaded = ser.LoadDeliveryReport(msg);
            msg.Release();

            Assert.That(loaded, Is.True);
            Assert.That(ser.CurrentDeliveryReport.Count, Is.EqualTo(1));
            Assert.That(ser.CurrentDeliveryReport[0].Id, Is.EqualTo(new DeliveryId(7)));
            Assert.That(ser.CurrentDeliveryReport[0].ChunkId, Is.EqualTo(3));
        }

        [Test]
        public void LoadDeliveryReport_MultipleConfirmations()
        {
            var ser = CreateSerializer();
            var confirmations = new List<DeliveryInfo>
            {
                new DeliveryInfo(new DeliveryId(1), 0),
                new DeliveryInfo(new DeliveryId(2), 1),
                new DeliveryInfo(new DeliveryId(3), 2)
            };
            var msg = ser.CreateDeliveryReport(confirmations, 0, 3);
            msg.AddRef();

            ser.LoadDeliveryReport(msg);
            msg.Release();

            Assert.That(ser.CurrentDeliveryReport.Count, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(ser.CurrentDeliveryReport[i].Id, Is.EqualTo(new DeliveryId((ushort)(i + 1))));
                Assert.That(ser.CurrentDeliveryReport[i].ChunkId, Is.EqualTo((byte)i));
            }
        }

        [Test]
        public void LoadDeliveryReport_WrongFirstElement_ReturnsFalse()
        {
            var ser = CreateSerializer();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)0xFF)); // not ushort count
            data.PutLast(new UnionData((ushort)1));
            data.PutLast(new UnionData((ushort)100));
            data.PutLast(new UnionData((byte)0));

            bool loaded = ser.LoadDeliveryReport(data);
            data.Release();
            Assert.That(loaded, Is.False);
            Assert.That(ser.CurrentDeliveryReport, Is.Empty);
        }

        [Test]
        public void LoadDeliveryReport_TruncatedAfterCount_ReturnsFalse()
        {
            var ser = CreateSerializer();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((ushort)5)); // count = 5, but no more elements

            bool loaded = ser.LoadDeliveryReport(data);
            data.Release();
            Assert.That(loaded, Is.False);
        }

        [Test]
        public void LoadDeliveryReport_CountZero_ReturnsTrueEmptyList()
        {
            var ser = CreateSerializer();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((ushort)0)); // count = 0

            bool loaded = ser.LoadDeliveryReport(data);
            data.Release();
            Assert.That(loaded, Is.True);
            Assert.That(ser.CurrentDeliveryReport, Is.Empty);
        }

        // ── Round-trip tests ──

        [Test]
        public void RoundTrip_DeliveryInfo()
        {
            var ser = CreateSerializer();
            var confirmations = new List<DeliveryInfo>
            {
                new DeliveryInfo(new DeliveryId(100), 5),
                new DeliveryInfo(new DeliveryId(200), 6)
            };
            var msg = ser.CreateDeliveryReport(confirmations, 0, 2);
            msg.AddRef();

            Assert.That(ser.LoadDeliveryReport(msg), Is.True);
            msg.Release();

            Assert.That(ser.CurrentDeliveryReport.Count, Is.EqualTo(2));
            Assert.That(ser.CurrentDeliveryReport[0].Id, Is.EqualTo(new DeliveryId(100)));
            Assert.That(ser.CurrentDeliveryReport[0].ChunkId, Is.EqualTo(5));
            Assert.That(ser.CurrentDeliveryReport[1].Id, Is.EqualTo(new DeliveryId(200)));
            Assert.That(ser.CurrentDeliveryReport[1].ChunkId, Is.EqualTo(6));
        }
    }
}
