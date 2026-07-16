using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.DeliveryManager;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager.Tests
{
    [Category("DeliveryManager")]
    public class UserMessageHandlerTests
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

        private static UserMessageHandler CreateHandler(int maxChunkSize = 100) =>
            new UserMessageHandler(Pool, CPool, maxChunkSize);

        // ── Overhead constant tests ──

        [Test]
        public void UserSingleOverhead_PlusSafetyMarginCoversActualSize()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserSingle(new DeliveryId(1), Data(0xAB, 0xCD));
            int overhead = msg.GetDataSize() - 2;
            int safety = 4;
            Assert.That(overhead, Is.LessThanOrEqualTo(UserMessageHandler.UserSingleOverhead + safety));
            msg.Release();
        }

        [Test]
        public void UserMultiOverhead_PlusSafetyMarginCoversActualSize()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserMulti(new DeliveryId(1), Data(0xAB), 0, 2);
            int overhead = msg.GetDataSize() - 1;
            int safety = 4;
            Assert.That(overhead, Is.LessThanOrEqualTo(UserMessageHandler.UserMultiOverhead + safety));
            msg.Release();
        }

        // ── CreateUserSingle tests ──

        [Test]
        public void CreateUserSingle_HasCorrectElementCount()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserSingle(new DeliveryId(42), Data(10, 20, 30));
            Assert.That(msg.Elements.Count, Is.EqualTo(3));
            msg.Release();
        }

        [Test]
        public void CreateUserSingle_Element0_IsTypeByte()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserSingle(new DeliveryId(7), Data(1));
            Assert.That(msg.Elements[0].Type, Is.EqualTo(UnionDataType.Byte));
            Assert.That(msg.Elements[0].Alias.ByteValue, Is.EqualTo(0));
            msg.Release();
        }

        [Test]
        public void CreateUserSingle_Element1_IsDeliveryId()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserSingle(new DeliveryId(0xABCD), Data(1));
            Assert.That(msg.Elements[1].Type, Is.EqualTo(UnionDataType.UShort));
            Assert.That(msg.Elements[1].Alias.UShortValue, Is.EqualTo(0xABCD));
            msg.Release();
        }

        [Test]
        public void CreateUserSingle_Element2_IsPayload()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserSingle(new DeliveryId(1), Data(1, 2, 3, 4));
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
            var handler = CreateHandler();
            var msg = handler.CreateUserSingle(new DeliveryId(1), Data());
            Assert.That(msg.Elements[2].Type, Is.EqualTo(UnionDataType.Array));
            Assert.That(msg.Elements[2].Bytes!.Count, Is.EqualTo(0));
            msg.Release();
        }

        // ── CreateUserMulti tests ──

        [Test]
        public void CreateUserMulti_HasCorrectElementCount()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserMulti(new DeliveryId(5), Data(99), 3, 7);
            Assert.That(msg.Elements.Count, Is.EqualTo(5));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element0_IsTypeByte()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserMulti(new DeliveryId(1), Data(1), 0, 1);
            Assert.That(msg.Elements[0].Alias.ByteValue, Is.EqualTo(1));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element1_IsDeliveryId()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserMulti(new DeliveryId(0x1234), Data(1), 0, 1);
            Assert.That(msg.Elements[1].Alias.UShortValue, Is.EqualTo(0x1234));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element2_IsPartId()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserMulti(new DeliveryId(1), Data(1), 5, 10);
            Assert.That(msg.Elements[2].Type, Is.EqualTo(UnionDataType.Byte));
            Assert.That(msg.Elements[2].Alias.ByteValue, Is.EqualTo(5));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element3_IsPartsNumber()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserMulti(new DeliveryId(1), Data(1), 0, 10);
            Assert.That(msg.Elements[3].Alias.ByteValue, Is.EqualTo(10));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_Element4_IsChunkData()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserMulti(new DeliveryId(1), Data(10, 20, 30), 0, 3);
            var payload = msg.Elements[4].Bytes!;
            var bytes = new byte[3];
            payload.CopyTo(bytes, 0, 0, 3);
            Assert.That(bytes, Is.EqualTo(new byte[] { 10, 20, 30 }));
            msg.Release();
        }

        [Test]
        public void CreateUserMulti_PartIdMaxValue_Works()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserMulti(new DeliveryId(1), Data(1), 255, 255);
            Assert.That(msg.Elements[2].Alias.ByteValue, Is.EqualTo(255));
            Assert.That(msg.Elements[3].Alias.ByteValue, Is.EqualTo(255));
            msg.Release();
        }

        // ── TryParseUserMessage tests ──

        [Test]
        public void TryParseUserMessage_UserSingle_ReturnsCorrectFields()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserSingle(new DeliveryId(0xDEAD), Data(1, 2, 3, 4, 5));
            msg.AddRef();

            bool parsed = handler.TryParseUserMessage(msg, out var result);
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
            var handler = CreateHandler();
            var msg = handler.CreateUserMulti(new DeliveryId(0xBE), Data(100, 200), 3, 8);
            msg.AddRef();

            bool parsed = handler.TryParseUserMessage(msg, out var result);
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
            var handler = CreateHandler();
            var msg = handler.CreateUserSingle(new DeliveryId(1), Data(1));
            msg.AddRef();
            handler.TryParseUserMessage(msg, out var result);
            msg.Release();
            Assert.That(result.IsMultiChunk, Is.False);
        }

        [Test]
        public void TryParseUserMessage_UserMulti_IsMultiChunkIsTrue()
        {
            var handler = CreateHandler();
            var msg = handler.CreateUserMulti(new DeliveryId(1), Data(1), 0, 1);
            msg.AddRef();
            handler.TryParseUserMessage(msg, out var result);
            msg.Release();
            Assert.That(result.IsMultiChunk, Is.True);
        }

        [Test]
        public void TryParseUserMessage_UnknownType_ReturnsFalse()
        {
            var handler = CreateHandler();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)0xFF));
            data.PutLast(new UnionData((ushort)1));
            data.PutLast(new UnionData(Data(1)));

            bool parsed = handler.TryParseUserMessage(data, out _);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseUserMessage_OnlyTypeByte_ReturnsFalse()
        {
            var handler = CreateHandler();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)0));

            bool parsed = handler.TryParseUserMessage(data, out _);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseUserMessage_MissingPayload_ReturnsFalse()
        {
            var handler = CreateHandler();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)0));
            data.PutLast(new UnionData((ushort)1));

            bool parsed = handler.TryParseUserMessage(data, out _);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseUserMessage_EmptyData_ReturnsFalse()
        {
            var handler = CreateHandler();
            var data = CPool.Acquire<UnionDataList>();

            bool parsed = handler.TryParseUserMessage(data, out _);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseUserMessage_Multi_MissingPartId_ReturnsFalse()
        {
            var handler = CreateHandler();
            var data = CPool.Acquire<UnionDataList>();
            data.PutLast(new UnionData((byte)1)); // TypeUserMulti
            data.PutLast(new UnionData((ushort)1)); // id
            // Missing partId and partsNumber and payload

            bool parsed = handler.TryParseUserMessage(data, out _);
            data.Release();
            Assert.That(parsed, Is.False);
        }

        // ── Round-trip tests ──

        [Test]
        public void RoundTrip_UserSingle()
        {
            var handler = CreateHandler();
            var originalId = new DeliveryId(0x1234);
            var originalData = new byte[] { 10, 20, 30, 40 };

            var msg = handler.CreateUserSingle(originalId, Data(originalData));
            msg.AddRef();

            Assert.That(handler.TryParseUserMessage(msg, out var parsed), Is.True);
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
            var handler = CreateHandler();
            var originalId = new DeliveryId(0x5678);
            var originalData = new byte[] { 1, 2, 3 };
            byte partId = 7;
            byte partsNumber = 10;

            var msg = handler.CreateUserMulti(originalId, Data(originalData), partId, partsNumber);
            msg.AddRef();

            Assert.That(handler.TryParseUserMessage(msg, out var parsed), Is.True);
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
            var handler = CreateHandler();
            var msg = handler.CreateUserSingle(DeliveryId.Zero, Data(42));
            msg.AddRef();

            Assert.That(handler.TryParseUserMessage(msg, out var parsed), Is.True);
            msg.Release();

            Assert.That(parsed.Id, Is.EqualTo(DeliveryId.Zero));
        }

        // ── Chunking: Combine tests (from MessageChunker) ──

        [Test]
        public void Combine_FirstChunk_ReturnsNull()
        {
            var handler = CreateHandler(10);
            var result = handler.Combine(new DeliveryId(1), 0, 2, Data(1));
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Combine_AllChunksInOrder_ReturnsCombined()
        {
            var handler = CreateHandler(10);
            var id = new DeliveryId(1);

            handler.Combine(id, 0, 2, Data(10, 20));
            var result = handler.Combine(id, 1, 2, Data(30, 40, 50));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(5));

            var bytes = new byte[5];
            result.CopyTo(bytes, 0, 0, 5);
            Assert.That(bytes, Is.EqualTo(new byte[] { 10, 20, 30, 40, 50 }));
            result.Release();
        }

        [Test]
        public void Combine_OutOfOrder_ReturnsCombined()
        {
            var handler = CreateHandler(10);
            var id = new DeliveryId(2);

            handler.Combine(id, 1, 2, Data(5, 6, 7));
            var result = handler.Combine(id, 0, 2, Data(1, 2));

            Assert.That(result.Count, Is.EqualTo(5));

            var bytes = new byte[5];
            result.CopyTo(bytes, 0, 0, 5);
            Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 5, 6, 7 }));
            result.Release();
        }

        [Test]
        public void Combine_DuplicateChunk_Ignored()
        {
            var handler = CreateHandler(10);
            var id = new DeliveryId(3);
            byte parts = 2;

            handler.Combine(id, 0, parts, Data(1, 2));
            handler.Combine(id, 0, parts, Data(99, 99));
            var result = handler.Combine(id, 1, parts, Data(3, 4));

            var bytes = new byte[4];
            result.CopyTo(bytes, 0, 0, 4);
            Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            result.Release();
        }

        [Test]
        public void Combine_InvalidPartId_Ignored()
        {
            var handler = CreateHandler(10);
            var id = new DeliveryId(4);

            var result = handler.Combine(id, 5, 2, Data(1));
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Clear_WithPending_PreventsLaterCombine()
        {
            var handler = CreateHandler(10);
            var id = new DeliveryId(5);

            handler.Combine(id, 0, 2, Data(1));
            handler.Clear();
            var result = handler.Combine(id, 1, 2, Data(2));

            Assert.That(result, Is.Null);
        }

        [Test]
        public void Combine_PartsNumber1_TreatedAsMulti()
        {
            var handler = CreateHandler(10);
            var result = handler.Combine(new DeliveryId(6), 0, 1, Data(42));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            result.Release();
        }

        [Test]
        public void Combine_DifferentIds_TrackedSeparately()
        {
            var handler = CreateHandler(10);

            handler.Combine(new DeliveryId(1), 0, 2, Data(1));
            handler.Combine(new DeliveryId(2), 0, 2, Data(10));
            var r1 = handler.Combine(new DeliveryId(1), 1, 2, Data(2));
            var r2 = handler.Combine(new DeliveryId(2), 1, 2, Data(20));

            var b1 = new byte[2];
            r1.CopyTo(b1, 0, 0, 2);
            Assert.That(b1, Is.EqualTo(new byte[] { 1, 2 }));

            var b2 = new byte[2];
            r2.CopyTo(b2, 0, 0, 2);
            Assert.That(b2, Is.EqualTo(new byte[] { 10, 20 }));

            r1.Release();
            r2.Release();
        }

        // ── Chunking: GetChunkCount tests ──

        [Test]
        public void GetChunkCount_ExactFit_ReturnsOne()
        {
            var handler = CreateHandler(5);
            Assert.That(handler.GetChunkCount(5), Is.EqualTo(1));
        }

        [Test]
        public void GetChunkCount_LargerThanMax_ReturnsMultiple()
        {
            var handler = CreateHandler(3);
            Assert.That(handler.GetChunkCount(7), Is.EqualTo(3));
        }

        [Test]
        public void GetChunkCount_Empty_ReturnsZero()
        {
            var handler = CreateHandler(10);
            Assert.That(handler.GetChunkCount(0), Is.EqualTo(0));
        }

        // ── Chunking: GetNextChunk tests ──

        [Test]
        public void GetNextChunk_IteratesAllChunks()
        {
            var handler = CreateHandler(3);
            var data = Data(1, 2, 3, 4, 5, 6, 7);

            int chunkId = 0;
            var all = new byte[7];
            while (handler.GetNextChunk(data, chunkId, out var chunk))
            {
                chunk.CopyTo(all, chunkId * 3, 0, chunk.Count);
                chunk.Release();
                chunkId += 1;
            }

            Assert.That(chunkId, Is.EqualTo(3));
            Assert.That(all, Is.EqualTo(new byte[] { 1, 2, 3, 4, 5, 6, 7 }));
        }

        [Test]
        public void GetNextChunk_PastEnd_ReturnsFalse()
        {
            var handler = CreateHandler(5);
            var data = Data(1, 2, 3);

            Assert.That(handler.GetNextChunk(data, 1, out _), Is.False);
            Assert.That(handler.GetNextChunk(data, 5, out _), Is.False);
        }

        [Test]
        public void GetNextChunk_EmptyData_ReturnsFalse()
        {
            var handler = CreateHandler(10);
            var data = Data();

            Assert.That(handler.GetNextChunk(data, 0, out _), Is.False);
        }
    }
}
