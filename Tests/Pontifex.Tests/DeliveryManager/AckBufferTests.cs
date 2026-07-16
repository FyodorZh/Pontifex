using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.DeliveryManager;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager.Tests
{
    [Category("DeliveryManager")]
    public class DeliveryRporterTests
    {
        private static IMemoryRental Memory => MemoryRental.Shared;
        private static IPool<IMultiRefByteArray, int> Pool => Memory.ByteArraysPool;
        private static ICollectablePool CPool => Memory.CollectablePool;

        private static DeliveryInfo Info(ushort id, byte chunk = 0) => new DeliveryInfo(new DeliveryId(id), chunk);

        // ── CreateDeliveryInfo tests ──

        [Test]
        public void CreateDeliveryInfo_SingleConfirmation()
        {
            var buffer = new DeliveryRporter(CPool);
            buffer.Add(Info(42, 7));

            var sent = new List<UnionDataList>();
            buffer.FlushDeliveryReports(100, 4, new ConsumerDelegate<UnionDataList>(x => { sent.Add(x); return true; }));

            Assert.That(sent.Count, Is.EqualTo(1));
            var msg = sent[0];
            Assert.That(msg.Elements.Count, Is.EqualTo(4));
            Assert.That(msg.Elements[0].Alias.ByteValue, Is.EqualTo(2)); // type = TypeDeliveryInfo
            Assert.That(msg.Elements[1].Alias.UShortValue, Is.EqualTo(1)); // count
            Assert.That(msg.Elements[2].Alias.UShortValue, Is.EqualTo(42)); // id
            Assert.That(msg.Elements[3].Alias.ByteValue, Is.EqualTo(7)); // chunkId
            msg.Release();
        }

        [Test]
        public void Flush_MultipleConfirmations()
        {
            var buffer = new DeliveryRporter(CPool);
            buffer.Add(Info(1, 0));
            buffer.Add(Info(2, 1));
            buffer.Add(Info(3, 2));

            var sent = new List<UnionDataList>();
            buffer.FlushDeliveryReports(100, 4, new ConsumerDelegate<UnionDataList>(x => { sent.Add(x); return true; }));

            Assert.That(sent.Count, Is.EqualTo(1));
            var msg = sent[0];
            Assert.That(msg.Elements.Count, Is.EqualTo(1 + 1 + 2 * 3));
            Assert.That(msg.Elements[1].Alias.UShortValue, Is.EqualTo(3));
            Assert.That(msg.Elements[2].Alias.UShortValue, Is.EqualTo(1));
            Assert.That(msg.Elements[3].Alias.ByteValue, Is.EqualTo(0));
            Assert.That(msg.Elements[4].Alias.UShortValue, Is.EqualTo(2));
            Assert.That(msg.Elements[5].Alias.ByteValue, Is.EqualTo(1));
            Assert.That(msg.Elements[6].Alias.UShortValue, Is.EqualTo(3));
            Assert.That(msg.Elements[7].Alias.ByteValue, Is.EqualTo(2));
            msg.Release();
        }

        [Test]
        public void Flush_BatchingAcrossMultiplePackets()
        {
            var buffer = new DeliveryRporter(CPool);
            // Flood enough confirmations to need multiple packets
            for (ushort i = 0; i < 30; i++)
                buffer.Add(Info(i, (byte)(i % 256)));

            var sent = new List<UnionDataList>();
            buffer.FlushDeliveryReports(100, 4, new ConsumerDelegate<UnionDataList>(x => { sent.Add(x); return true; }));

            Assert.That(sent.Count, Is.GreaterThan(1));
            int totalConfirmations = 0;
            foreach (var msg in sent)
            {
                totalConfirmations += msg.Elements[1].Alias.UShortValue;
                msg.Release();
            }
            Assert.That(totalConfirmations, Is.EqualTo(30));
        }

        [Test]
        public void Flush_Empty_DoesNothing()
        {
            var buffer = new DeliveryRporter(CPool);
            bool called = false;

            buffer.FlushDeliveryReports(100, 4, new ConsumerDelegate<UnionDataList>(_ => { called = true; return true; }));

            Assert.That(called, Is.False);
        }

        [Test]
        public void Flush_ClearsAfterSending()
        {
            var buffer = new DeliveryRporter(CPool);
            buffer.Add(Info(1));
            buffer.FlushDeliveryReports(100, 4, new ConsumerDelegate<UnionDataList>(_ => true));

            bool called = false;
            buffer.FlushDeliveryReports(100, 4, new ConsumerDelegate<UnionDataList>(_ => { called = true; return true; }));
            Assert.That(called, Is.False);
        }

        // ── TryParseDeliveryInfo tests ──

        [Test]
        public void TryParseDeliveryInfo_ParseValid_ReturnsTrueAndPopulatesList()
        {
            var buffer = new DeliveryRporter(CPool);
            buffer.Add(Info(7, 3));

            var sent = new List<UnionDataList>();
            buffer.FlushDeliveryReports(100, 4, new ConsumerDelegate<UnionDataList>(x => { sent.Add(x); return true; }));

            var msg = sent[0];
            msg.AddRef();

            var result = new List<DeliveryInfo>();
            bool parsed = buffer.ParseDeliveryReport(msg, result);
            msg.Release();

            Assert.That(parsed, Is.True);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo(new DeliveryId(7)));
            Assert.That(result[0].ChunkId, Is.EqualTo(3));
        }

        [Test]
        public void TryParseDeliveryInfo_MultipleConfirmations()
        {
            var buffer = new DeliveryRporter(CPool);
            buffer.Add(Info(1, 0));
            buffer.Add(Info(2, 1));
            buffer.Add(Info(3, 2));

            var sent = new List<UnionDataList>();
            buffer.FlushDeliveryReports(100, 4, new ConsumerDelegate<UnionDataList>(x => { sent.Add(x); return true; }));

            var msg = sent[0];
            msg.AddRef();

            var result = new List<DeliveryInfo>();
            buffer.ParseDeliveryReport(msg, result);
            msg.Release();

            Assert.That(result.Count, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(result[i].Id, Is.EqualTo(new DeliveryId((ushort)(i + 1))));
                Assert.That(result[i].ChunkId, Is.EqualTo((byte)i));
            }
        }

        [Test]
        public void TryParseDeliveryInfo_WrongTypeByte_ReturnsFalse()
        {
            var buffer = new DeliveryRporter(CPool);
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)0xFF)); // not TypeDeliveryInfo
            data.PutLast(new UnionData((ushort)1));
            data.PutLast(new UnionData((ushort)100));
            data.PutLast(new UnionData((byte)0));

            var result = new List<DeliveryInfo>();
            bool parsed = buffer.ParseDeliveryReport(data, result);
            data.Release();
            Assert.That(parsed, Is.False);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void TryParseDeliveryInfo_TruncatedAfterCount_ReturnsFalse()
        {
            var buffer = new DeliveryRporter(CPool);
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)2)); // TypeDeliveryInfo
            data.PutLast(new UnionData((ushort)5)); // count = 5, but no more elements

            var result = new List<DeliveryInfo>();
            bool parsed = buffer.ParseDeliveryReport(data, result);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseDeliveryInfo_CountZero_ReturnsTrueEmptyList()
        {
            var buffer = new DeliveryRporter(CPool);
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)2)); // TypeDeliveryInfo
            data.PutLast(new UnionData((ushort)0)); // count = 0

            var result = new List<DeliveryInfo>();
            bool parsed = buffer.ParseDeliveryReport(data, result);
            data.Release();
            Assert.That(parsed, Is.True);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void TryParseDeliveryInfo_EmptyData_ReturnsFalse()
        {
            var buffer = new DeliveryRporter(CPool);
            var data = CPool.Acquire<UnionDataList>();
            var result = new List<DeliveryInfo>();
            bool parsed = buffer.ParseDeliveryReport(data, result);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        // ── Round-trip ──

        [Test]
        public void RoundTrip_DeliveryInfo()
        {
            var buffer = new DeliveryRporter(CPool);
            buffer.Add(Info(100, 5));
            buffer.Add(Info(200, 6));

            var sent = new List<UnionDataList>();
            buffer.FlushDeliveryReports(100, 4, new ConsumerDelegate<UnionDataList>(x => { sent.Add(x); return true; }));

            var msg = sent[0];
            msg.AddRef();

            var result = new List<DeliveryInfo>();
            Assert.That(buffer.ParseDeliveryReport(msg, result), Is.True);
            msg.Release();

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Id, Is.EqualTo(new DeliveryId(100)));
            Assert.That(result[0].ChunkId, Is.EqualTo(5));
            Assert.That(result[1].Id, Is.EqualTo(new DeliveryId(200)));
            Assert.That(result[1].ChunkId, Is.EqualTo(6));
        }

        // ── Clear ──

        [Test]
        public void Clear_EmptiesBuffer()
        {
            var buffer = new DeliveryRporter(CPool);
            buffer.Add(Info(1));
            buffer.Clear();

            bool called = false;
            buffer.FlushDeliveryReports(100, 4, new ConsumerDelegate<UnionDataList>(_ => { called = true; return true; }));
            Assert.That(called, Is.False);
        }
    }
}
