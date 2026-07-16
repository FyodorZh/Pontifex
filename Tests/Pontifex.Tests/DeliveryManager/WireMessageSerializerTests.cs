using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.DeliveryManager;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager.Tests
{
    [Category("DeliveryManager")]
    public class WireMessageSerializerTests
    {
        private static IMemoryRental Memory => MemoryRental.Shared;
        private static IPool<IMultiRefByteArray, int> Pool => Memory.ByteArraysPool;
        private static ICollectablePool CPool => Memory.CollectablePool;

        private static IMultiRefByteArray Data(params byte[] bytes)
        {
            var buf = Pool.Acquire(bytes.Length);
            Buffer.BlockCopy(bytes, 0, buf.Array, buf.Offset, bytes.Length);
            return buf;
        }

        private static WireMessageSerializer CreateSerializer() => new WireMessageSerializer(CPool);

        // ── Overhead constant tests ──

        [Test]
        public void UserSingleOverhead_PlusSafetyMarginCoversActualSize()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(new DeliveryId(1), Data(0xAB, 0xCD));
            int overhead = msg.GetDataSize() - 2;
            int safety = 4;
            Assert.That(overhead, Is.LessThanOrEqualTo(ser.UserSingleOverhead + safety));
            msg.Release();
        }

        [Test]
        public void UserMultiOverhead_PlusSafetyMarginCoversActualSize()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(1), Data(0xAB), 0, 2);
            int overhead = msg.GetDataSize() - 1;
            int safety = 4;
            Assert.That(overhead, Is.LessThanOrEqualTo(ser.UserMultiOverhead + safety));
            msg.Release();
        }

        // ── CreateUserSingle tests ──

        [Test]
        public void CreateUserSingle_HasCorrectElementCount()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(new DeliveryId(42), Data(10, 20, 30));
            Assert.That(msg.Elements.Count, Is.EqualTo(3));
            msg.Release();
        }

        [Test]
        public void CreateUserSingle_Element0_IsTypeByte()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(new DeliveryId(7), Data(1));
            Assert.That(msg.Elements[0].Type, Is.EqualTo(UnionDataType.Byte));
            Assert.That(msg.Elements[0].Alias.ByteValue, Is.EqualTo(0));
            msg.Release();
        }

        [Test]
        public void CreateUserSingle_Element1_IsDeliveryId()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(new DeliveryId(0xABCD), Data(1));
            Assert.That(msg.Elements[1].Type, Is.EqualTo(UnionDataType.UShort));
            Assert.That(msg.Elements[1].Alias.UShortValue, Is.EqualTo(0xABCD));
            msg.Release();
        }

        [Test]
        public void CreateUserSingle_Element2_IsPayload()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(new DeliveryId(1), Data(1, 2, 3, 4));
            Assert.That(msg.Elements[2].Type, Is.EqualTo(UnionDataType.Array));
            var payload = msg.Elements[2].Bytes!;
            Assert.That(payload.Count, Is.EqualTo(4));
            var bytes = new byte[4];
            payload.CopyTo(bytes, 0, 0, 4);
            Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            msg.Release();
        }

        [Test]
        public void CreateUserSingle_EmptyPayload_Works()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(new DeliveryId(1), Data());
            Assert.That(msg.Elements[2].Type, Is.EqualTo(UnionDataType.Array));
            Assert.That(msg.Elements[2].Bytes!.Count, Is.EqualTo(0));
            msg.Release();
        }

        // ── CreateUserMulti tests ──

        [Test]
        public void CreateUserMulti_HasCorrectElementCount()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(5), Data(99), 3, 7);
            Assert.That(msg.Elements.Count, Is.EqualTo(5));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element0_IsTypeByte()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(1), Data(1), 0, 1);
            Assert.That(msg.Elements[0].Alias.ByteValue, Is.EqualTo(1));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element1_IsDeliveryId()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(0x1234), Data(1), 0, 1);
            Assert.That(msg.Elements[1].Alias.UShortValue, Is.EqualTo(0x1234));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element2_IsPartId()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(1), Data(1), 5, 10);
            Assert.That(msg.Elements[2].Type, Is.EqualTo(UnionDataType.Byte));
            Assert.That(msg.Elements[2].Alias.ByteValue, Is.EqualTo(5));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element3_IsPartsNumber()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(1), Data(1), 0, 10);
            Assert.That(msg.Elements[3].Alias.ByteValue, Is.EqualTo(10));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element4_IsChunkData()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(1), Data(10, 20, 30), 0, 3);
            var payload = msg.Elements[4].Bytes!;
            var bytes = new byte[3];
            payload.CopyTo(bytes, 0, 0, 3);
            Assert.That(bytes, Is.EqualTo(new byte[] { 10, 20, 30 }));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_PartIdMaxValue_Works()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(1), Data(1), 255, 255);
            Assert.That(msg.Elements[2].Alias.ByteValue, Is.EqualTo(255));
            Assert.That(msg.Elements[3].Alias.ByteValue, Is.EqualTo(255));
            msg.Release();
        }

        // ── CreateDeliveryInfo tests ──

        [Test]
        public void CreateDeliveryInfo_SingleConfirmation()
        {
            var ser = CreateSerializer();
            var confirmations = new List<DeliveryInfo> { new DeliveryInfo(new DeliveryId(42), 7) };
            var msg = ser.CreateDeliveryInfo(confirmations, 0, 1);
            Assert.That(msg.Elements.Count, Is.EqualTo(5));
            Assert.That(msg.Elements[0].Alias.UShortValue, Is.EqualTo(0)); // packetId
            Assert.That(msg.Elements[1].Alias.ByteValue, Is.EqualTo(2)); // type = TypeDeliveryInfo
            Assert.That(msg.Elements[2].Alias.UShortValue, Is.EqualTo(1)); // count
            Assert.That(msg.Elements[3].Alias.UShortValue, Is.EqualTo(42)); // id
            Assert.That(msg.Elements[4].Alias.ByteValue, Is.EqualTo(7)); // chunkId
            msg.Release();
        }

        [Test]
        public void CreateDeliveryInfo_MultipleConfirmations()
        {
            var ser = CreateSerializer();
            var confirmations = new List<DeliveryInfo>
            {
                new DeliveryInfo(new DeliveryId(1), 0),
                new DeliveryInfo(new DeliveryId(2), 1),
                new DeliveryInfo(new DeliveryId(3), 2)
            };
            var msg = ser.CreateDeliveryInfo(confirmations, 0, 3);
            Assert.That(msg.Elements.Count, Is.EqualTo(3 + 2 * 3));
            Assert.That(msg.Elements[2].Alias.UShortValue, Is.EqualTo(3));

            Assert.That(msg.Elements[3].Alias.UShortValue, Is.EqualTo(1));
            Assert.That(msg.Elements[4].Alias.ByteValue, Is.EqualTo(0));
            Assert.That(msg.Elements[5].Alias.UShortValue, Is.EqualTo(2));
            Assert.That(msg.Elements[6].Alias.ByteValue, Is.EqualTo(1));
            Assert.That(msg.Elements[7].Alias.UShortValue, Is.EqualTo(3));
            Assert.That(msg.Elements[8].Alias.ByteValue, Is.EqualTo(2));
            msg.Release();
        }

        [Test]
        public void CreateDeliveryInfo_PartialRange()
        {
            var ser = CreateSerializer();
            var confirmations = new List<DeliveryInfo>
            {
                new DeliveryInfo(new DeliveryId(10), 0),
                new DeliveryInfo(new DeliveryId(20), 1),
                new DeliveryInfo(new DeliveryId(30), 2)
            };
            var msg = ser.CreateDeliveryInfo(confirmations, 1, 2);
            Assert.That(msg.Elements[2].Alias.UShortValue, Is.EqualTo(2));
            Assert.That(msg.Elements[3].Alias.UShortValue, Is.EqualTo(20));
            Assert.That(msg.Elements[5].Alias.UShortValue, Is.EqualTo(30));
            msg.Release();
        }

        [Test]
        public void CreateDeliveryInfo_ZeroCount()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateDeliveryInfo(new List<DeliveryInfo>(), 0, 0);
            Assert.That(msg.Elements.Count, Is.EqualTo(3));
            Assert.That(msg.Elements[2].Alias.UShortValue, Is.EqualTo(0));
            msg.Release();
        }

        // ── TryParseUserMessage tests ──

        [Test]
        public void TryParseUserMessage_UserSingle_ReturnsCorrectFields()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(new DeliveryId(0xDEAD), Data(1, 2, 3, 4, 5));
            msg.AddRef();

            bool parsed = ser.TryParseUserMessage(msg, out var result);
            msg.Release();

            Assert.That(parsed, Is.True);
            Assert.That(result.Id, Is.EqualTo(new DeliveryId(0xDEAD)));
            Assert.That(result.IsMultiChunk, Is.False);
            Assert.That(result.PartId, Is.EqualTo(0));
            Assert.That(result.PartsNumber, Is.EqualTo(0));
            Assert.That(result.Payload.Count, Is.EqualTo(5));
        }

        [Test]
        public void TryParseUserMessage_UserMulti_ReturnsCorrectFields()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(0xBE), Data(100, 200), 3, 8);
            msg.AddRef();

            bool parsed = ser.TryParseUserMessage(msg, out var result);
            msg.Release();

            Assert.That(parsed, Is.True);
            Assert.That(result.Id, Is.EqualTo(new DeliveryId(0xBE)));
            Assert.That(result.IsMultiChunk, Is.True);
            Assert.That(result.PartId, Is.EqualTo(3));
            Assert.That(result.PartsNumber, Is.EqualTo(8));
            Assert.That(result.Payload.Count, Is.EqualTo(2));
        }

        [Test]
        public void TryParseUserMessage_UserSingle_IsMultiChunkIsFalse()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(new DeliveryId(1), Data(1));
            msg.AddRef();
            ser.TryParseUserMessage(msg, out var result);
            msg.Release();
            Assert.That(result.IsMultiChunk, Is.False);
        }

        [Test]
        public void TryParseUserMessage_UserMulti_IsMultiChunkIsTrue()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(1), Data(1), 0, 1);
            msg.AddRef();
            ser.TryParseUserMessage(msg, out var result);
            msg.Release();
            Assert.That(result.IsMultiChunk, Is.True);
        }

        [Test]
        public void TryParseUserMessage_UnknownType_ReturnsFalse()
        {
            var ser = CreateSerializer();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)0xFF));
            data.PutLast(new UnionData((ushort)1));
            data.PutLast(new UnionData(Data(1)));

            bool parsed = ser.TryParseUserMessage(data, out _);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseUserMessage_OnlyTypeByte_ReturnsFalse()
        {
            var ser = CreateSerializer();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)0));

            bool parsed = ser.TryParseUserMessage(data, out _);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseUserMessage_MissingPayload_ReturnsFalse()
        {
            var ser = CreateSerializer();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)0));
            data.PutLast(new UnionData((ushort)1));

            bool parsed = ser.TryParseUserMessage(data, out _);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseUserMessage_EmptyData_ReturnsFalse()
        {
            var ser = CreateSerializer();
            var data = CPool.Acquire<UnionDataList>();

            bool parsed = ser.TryParseUserMessage(data, out _);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseUserMessage_Multi_MissingPartId_ReturnsFalse()
        {
            var ser = CreateSerializer();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)1)); // TypeUserMulti
            data.PutLast(new UnionData((ushort)1)); // id
            // Missing partId and partsNumber and payload

            bool parsed = ser.TryParseUserMessage(data, out _);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        // ── TryParseDeliveryInfo tests ──

        [Test]
        public void TryParseDeliveryInfo_ParseValid_ReturnsTrueAndPopulatesList()
        {
            var ser = CreateSerializer();
            var confirmations = new List<DeliveryInfo>
            {
                new DeliveryInfo(new DeliveryId(7), 3)
            };
            var msg = ser.CreateDeliveryInfo(confirmations, 0, 1);
            msg.TryPopFirst(out ushort _); // pop packetId
            msg.AddRef();

            var result = new List<DeliveryInfo>();
            bool parsed = ser.TryParseDeliveryInfo(msg, result);
            msg.Release();

            Assert.That(parsed, Is.True);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo(new DeliveryId(7)));
            Assert.That(result[0].ChunkId, Is.EqualTo(3));
        }

        [Test]
        public void TryParseDeliveryInfo_MultipleConfirmations()
        {
            var ser = CreateSerializer();
            var confirmations = new List<DeliveryInfo>
            {
                new DeliveryInfo(new DeliveryId(1), 0),
                new DeliveryInfo(new DeliveryId(2), 1),
                new DeliveryInfo(new DeliveryId(3), 2)
            };
            var msg = ser.CreateDeliveryInfo(confirmations, 0, 3);
            msg.TryPopFirst(out ushort _); // pop packetId
            msg.AddRef();

            var result = new List<DeliveryInfo>();
            ser.TryParseDeliveryInfo(msg, result);
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
            var ser = CreateSerializer();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)0xFF)); // not TypeDeliveryInfo
            data.PutLast(new UnionData((ushort)1));
            data.PutLast(new UnionData((ushort)100));
            data.PutLast(new UnionData((byte)0));

            var result = new List<DeliveryInfo>();
            bool parsed = ser.TryParseDeliveryInfo(data, result);
            data.Release();
            Assert.That(parsed, Is.False);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void TryParseDeliveryInfo_TruncatedAfterCount_ReturnsFalse()
        {
            var ser = CreateSerializer();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)2)); // TypeDeliveryInfo
            data.PutLast(new UnionData((ushort)5)); // count = 5, but no more elements

            var result = new List<DeliveryInfo>();
            bool parsed = ser.TryParseDeliveryInfo(data, result);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseDeliveryInfo_CountZero_ReturnsTrueEmptyList()
        {
            var ser = CreateSerializer();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)2)); // TypeDeliveryInfo
            data.PutLast(new UnionData((ushort)0)); // count = 0

            var result = new List<DeliveryInfo>();
            bool parsed = ser.TryParseDeliveryInfo(data, result);
            data.Release();
            Assert.That(parsed, Is.True);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void TryParseDeliveryInfo_EmptyData_ReturnsFalse()
        {
            var ser = CreateSerializer();
            var data = CPool.Acquire<UnionDataList>();
            var result = new List<DeliveryInfo>();
            bool parsed = ser.TryParseDeliveryInfo(data, result);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        // ── Round-trip tests ──

        [Test]
        public void RoundTrip_UserSingle()
        {
            var ser = CreateSerializer();
            var originalId = new DeliveryId(0x1234);
            var originalData = new byte[] { 10, 20, 30, 40 };

            var msg = ser.CreateUserSingle(originalId, Data(originalData));
            msg.AddRef();

            Assert.That(ser.TryParseUserMessage(msg, out var parsed), Is.True);
            msg.Release();

            Assert.That(parsed.Id, Is.EqualTo(originalId));
            Assert.That(parsed.IsMultiChunk, Is.False);

            var resultBytes = new byte[parsed.Payload.Count];
            parsed.Payload.CopyTo(resultBytes, 0, 0, parsed.Payload.Count);
            Assert.That(resultBytes, Is.EqualTo(originalData));
        }

        [Test]
        public void RoundTrip_UserMulti()
        {
            var ser = CreateSerializer();
            var originalId = new DeliveryId(0x5678);
            var originalData = new byte[] { 1, 2, 3 };
            byte partId = 7;
            byte partsNumber = 10;

            var msg = ser.CreateUserMulti(originalId, Data(originalData), partId, partsNumber);
            msg.AddRef();

            Assert.That(ser.TryParseUserMessage(msg, out var parsed), Is.True);
            msg.Release();

            Assert.That(parsed.Id, Is.EqualTo(originalId));
            Assert.That(parsed.IsMultiChunk, Is.True);
            Assert.That(parsed.PartId, Is.EqualTo(partId));
            Assert.That(parsed.PartsNumber, Is.EqualTo(partsNumber));

            var resultBytes = new byte[parsed.Payload.Count];
            parsed.Payload.CopyTo(resultBytes, 0, 0, parsed.Payload.Count);
            Assert.That(resultBytes, Is.EqualTo(originalData));
        }

        [Test]
        public void RoundTrip_DeliveryInfo()
        {
            var ser = CreateSerializer();
            var confirmations = new List<DeliveryInfo>
            {
                new DeliveryInfo(new DeliveryId(100), 5),
                new DeliveryInfo(new DeliveryId(200), 6)
            };
            var msg = ser.CreateDeliveryInfo(confirmations, 0, 2);
            msg.TryPopFirst(out ushort _); // pop packetId
            msg.AddRef();

            var result = new List<DeliveryInfo>();
            Assert.That(ser.TryParseDeliveryInfo(msg, result), Is.True);
            msg.Release();

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Id, Is.EqualTo(new DeliveryId(100)));
            Assert.That(result[0].ChunkId, Is.EqualTo(5));
            Assert.That(result[1].Id, Is.EqualTo(new DeliveryId(200)));
            Assert.That(result[1].ChunkId, Is.EqualTo(6));
        }

        [Test]
        public void RoundTrip_UserSingle_ZeroDeliveryId()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(DeliveryId.Zero, Data(42));
            msg.AddRef();

            Assert.That(ser.TryParseUserMessage(msg, out var parsed), Is.True);
            msg.Release();

            Assert.That(parsed.Id, Is.EqualTo(DeliveryId.Zero));
        }
    }
}
