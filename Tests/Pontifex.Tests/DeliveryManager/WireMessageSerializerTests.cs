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
        public void CreateUserSingle_Element0_IsPartsNumber()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(new DeliveryId(7), Data(1));
            Assert.That(msg.Elements[0].Type, Is.EqualTo(UnionDataType.Byte));
            Assert.That(msg.Elements[0].Alias.ByteValue, Is.EqualTo(1)); // single chunk
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
            Assert.That(msg.Elements.Count, Is.EqualTo(4));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element0_IsPartsNumber()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(1), Data(1), 0, 7);
            Assert.That(msg.Elements[0].Type, Is.EqualTo(UnionDataType.Byte));
            Assert.That(msg.Elements[0].Alias.ByteValue, Is.EqualTo(7));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element1_IsPartId()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(1), Data(1), 5, 10);
            Assert.That(msg.Elements[1].Type, Is.EqualTo(UnionDataType.Byte));
            Assert.That(msg.Elements[1].Alias.ByteValue, Is.EqualTo(5));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element2_IsDeliveryId()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(0x1234), Data(1), 0, 1);
            Assert.That(msg.Elements[2].Type, Is.EqualTo(UnionDataType.UShort));
            Assert.That(msg.Elements[2].Alias.UShortValue, Is.EqualTo(0x1234));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element3_IsChunkData()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(1), Data(10, 20, 30), 0, 3);
            var payload = msg.Elements[3].Bytes!;
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
            Assert.That(msg.Elements[0].Alias.ByteValue, Is.EqualTo(255)); // partsNumber
            Assert.That(msg.Elements[1].Alias.ByteValue, Is.EqualTo(255)); // partId
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
            Assert.That(result.PartsNumber, Is.EqualTo(1));
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
            var msg = ser.CreateUserMulti(new DeliveryId(1), Data(1), 0, 2);
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
            data.PutLast(new UnionData((byte)2)); // partsNumber = 2 (multi-chunk)
            data.PutLast(new UnionData((ushort)1)); // expected partId (byte), but got ushort

            bool parsed = ser.TryParseUserMessage(data, out _);
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
        public void RoundTrip_UserSingle_ZeroDeliveryId()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(DeliveryId.Zero, Data(42));
            msg.AddRef();

            Assert.That(ser.TryParseUserMessage(msg, out var parsed), Is.True);
            msg.Release();

            Assert.That(parsed.Id, Is.EqualTo(DeliveryId.Zero));
        }

        // ════════════════════════════════════════════════════════════════
        //  New wire format target tests
        //  Target: serializer produces tail elements (without discriminator/wireChunkId),
        //  the dispatcher/collector prepends the head.
        //
        //  CreateUserSingle → [byte(partsNumber=1), ushort(deliveryId), array]
        //  CreateUserMulti  → [byte(partsNum), byte(partId), ushort(deliveryId), array]
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void NewFormat_CreateUserSingle_Structure()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(new DeliveryId(42), Data(10, 20, 30));

            Assert.That(msg.Elements.Count, Is.EqualTo(3));
            Assert.That(msg.Elements[0].Type, Is.EqualTo(UnionDataType.Byte));   // partsNumber
            Assert.That(msg.Elements[0].Alias.ByteValue, Is.EqualTo(1));
            Assert.That(msg.Elements[1].Type, Is.EqualTo(UnionDataType.UShort)); // deliveryId
            Assert.That(msg.Elements[1].Alias.UShortValue, Is.EqualTo(42));
            Assert.That(msg.Elements[2].Type, Is.EqualTo(UnionDataType.Array));  // payload
            msg.Release();
        }

        [Test]
        public void NewFormat_CreateUserMulti_Structure()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(7), Data(100, 200), 3, 8);

            Assert.That(msg.Elements.Count, Is.EqualTo(4));
            Assert.That(msg.Elements[0].Type, Is.EqualTo(UnionDataType.Byte));   // partsNumber
            Assert.That(msg.Elements[0].Alias.ByteValue, Is.EqualTo(8));
            Assert.That(msg.Elements[1].Type, Is.EqualTo(UnionDataType.Byte));   // partId
            Assert.That(msg.Elements[1].Alias.ByteValue, Is.EqualTo(3));
            Assert.That(msg.Elements[2].Type, Is.EqualTo(UnionDataType.UShort)); // deliveryId
            Assert.That(msg.Elements[2].Alias.UShortValue, Is.EqualTo(7));
            Assert.That(msg.Elements[3].Type, Is.EqualTo(UnionDataType.Array));  // chunk data
            msg.Release();
        }

        [Test]
        public void NewFormat_TryParseUserMessage_Single()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(new DeliveryId(0xABCD), Data(1, 2, 3, 4, 5));
            msg.AddRef();

            bool parsed = ser.TryParseUserMessage(msg, out var result);
            msg.Release();

            Assert.That(parsed, Is.True);
            Assert.That(result.Id, Is.EqualTo(new DeliveryId(0xABCD)));
            Assert.That(result.IsMultiChunk, Is.False);
            Assert.That(result.PartId, Is.EqualTo(0));
            Assert.That(result.PartsNumber, Is.EqualTo(1));
            Assert.That(result.Payload.Count, Is.EqualTo(5));
        }

        [Test]
        public void NewFormat_TryParseUserMessage_Multi()
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
        public void NewFormat_TryParseUserMessage_Single_IsMultiChunkFalse()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserSingle(new DeliveryId(1), Data(1));
            msg.AddRef();
            ser.TryParseUserMessage(msg, out var result);
            msg.Release();
            Assert.That(result.IsMultiChunk, Is.False);
        }

        [Test]
        public void NewFormat_TryParseUserMessage_Multi_IsMultiChunkTrue()
        {
            var ser = CreateSerializer();
            var msg = ser.CreateUserMulti(new DeliveryId(1), Data(1), 0, 2);
            msg.AddRef();
            ser.TryParseUserMessage(msg, out var result);
            msg.Release();
            Assert.That(result.IsMultiChunk, Is.True);
        }

        [Test]
        public void NewFormat_TryParseUserMessage_InvalidFirstElement_ReturnsFalse()
        {
            // If the first element is not a byte (partsNumber), parsing fails
            var ser = CreateSerializer();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((ushort)0xFFFF)); // not byte
            data.PutLast(new UnionData((ushort)1));
            data.PutLast(new UnionData(Data(1)));

            bool parsed = ser.TryParseUserMessage(data, out _);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        [Test]
        public void NewFormat_RoundTrip_UserSingle()
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
            Assert.That(parsed.PartsNumber, Is.EqualTo(1));

            var resultBytes = new byte[parsed.Payload.Count];
            parsed.Payload.CopyTo(resultBytes, 0, 0, parsed.Payload.Count);
            Assert.That(resultBytes, Is.EqualTo(originalData));
        }

        [Test]
        public void NewFormat_RoundTrip_UserMulti()
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

    }
}
