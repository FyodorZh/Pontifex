using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.DeliveryManager;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager.Tests
{
    [Category("DeliveryManager")]
    public class MessagePackerTests
    {
        private const int MaxMsgSize = 100;
        private const int SafetyMargin = 4;
        private static IMemoryRental Memory => MemoryRental.Shared;
        private static IPool<IMultiRefByteArray, int> Pool => Memory.ByteArraysPool;
        private static ICollectablePool CPool => Memory.CollectablePool;

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

        private static (MessagePacker packer, IWireMessageSerializer serializer, MessageChunker chunker) CreatePackEnvironment()
        {
            var serializer = new WireMessageSerializer(CPool);
            int singleMax = MaxMsgSize - serializer.UserSingleOverhead - SafetyMargin;
            int multiMax = MaxMsgSize - serializer.UserMultiOverhead - SafetyMargin;
            var chunker = new MessageChunker(Pool, multiMax);
            var packer = new MessagePacker(Pool, CPool, serializer, chunker, singleMax, multiMax);
            return (packer, serializer, chunker);
        }

        // ── DeliveryMaxByteSize ──

        [Test]
        public void DeliveryMaxByteSize_ComputedCorrectly()
        {
            var (packer, serializer, _) = CreatePackEnvironment();
            int multiMax = MaxMsgSize - serializer.UserMultiOverhead - SafetyMargin;
            Assert.That(packer.DeliveryMaxByteSize, Is.EqualTo(multiMax * 255));
        }

        // ── Single-chunk tests ──

        [Test]
        public void Pack_SmallData_ProducesOneQueuedMessage()
        {
            var (packer, _, _) = CreatePackEnvironment();
            var sent = new List<QueuedMessage>();

            var result = packer.Pack(new DeliveryId(1), DataList(1, 2, 3),
                new ConsumerDelegate<QueuedMessage>(x => { sent.Add(x); return true; }));

            Assert.That(result, Is.EqualTo(SendResult.Ok));
            Assert.That(sent.Count, Is.EqualTo(1));
            foreach (var qm in sent) qm.Data.Release();
        }

        [Test]
        public void Pack_SingleChunk_HasCorrectDeliveryId()
        {
            var (packer, _, _) = CreatePackEnvironment();
            var sent = new List<QueuedMessage>();

            packer.Pack(new DeliveryId(42), DataList(1, 2, 3),
                new ConsumerDelegate<QueuedMessage>(x => { sent.Add(x); return true; }));

            Assert.That(sent[0].Info.Id, Is.EqualTo(new DeliveryId(42)));
            Assert.That(sent[0].Info.ChunkId, Is.EqualTo(0));
            sent[0].Data.Release();
        }

        [Test]
        public void Pack_SingleChunk_EmptyData_Works()
        {
            var (packer, _, _) = CreatePackEnvironment();
            var sent = new List<QueuedMessage>();

            var result = packer.Pack(new DeliveryId(1), DataList(),
                new ConsumerDelegate<QueuedMessage>(x => { sent.Add(x); return true; }));

            Assert.That(result, Is.EqualTo(SendResult.Ok));
            Assert.That(sent.Count, Is.EqualTo(1));
            sent[0].Data.Release();
        }

        // ── Multi-chunk tests ──

        [Test]
        public void Pack_LargeData_ProducesMultipleQueuedMessages()
        {
            var (packer, _, _) = CreatePackEnvironment();
            var sent = new List<QueuedMessage>();
            var data = new byte[200];
            for (int i = 0; i < 200; i++) data[i] = (byte)i;

            var result = packer.Pack(new DeliveryId(1), DataList(data),
                new ConsumerDelegate<QueuedMessage>(x => { sent.Add(x); return true; }));

            Assert.That(result, Is.EqualTo(SendResult.Ok));
            Assert.That(sent.Count, Is.GreaterThan(1));
            foreach (var qm in sent) qm.Data.Release();
        }

        [Test]
        public void Pack_MultiChunk_AllHaveSameDeliveryId()
        {
            var (packer, _, _) = CreatePackEnvironment();
            var sent = new List<QueuedMessage>();
            var data = new byte[200];

            packer.Pack(new DeliveryId(7), DataList(data),
                new ConsumerDelegate<QueuedMessage>(x => { sent.Add(x); return true; }));

            foreach (var qm in sent)
                Assert.That(qm.Info.Id, Is.EqualTo(new DeliveryId(7)));
            foreach (var qm in sent) qm.Data.Release();
        }

        [Test]
        public void Pack_MultiChunk_ChunkIdsAreSequential()
        {
            var (packer, _, _) = CreatePackEnvironment();
            var sent = new List<QueuedMessage>();
            var data = new byte[200];

            packer.Pack(new DeliveryId(1), DataList(data),
                new ConsumerDelegate<QueuedMessage>(x => { sent.Add(x); return true; }));

            for (int i = 0; i < sent.Count; i++)
                Assert.That(sent[i].Info.ChunkId, Is.EqualTo((byte)i));
            foreach (var qm in sent) qm.Data.Release();
        }

        [Test]
        public void Pack_MultiChunk_PayloadSizesSumToOriginal()
        {
            var (packer, serializer, _) = CreatePackEnvironment();
            var sent = new List<QueuedMessage>();
            var originalBytes = new byte[200];
            for (int i = 0; i < 200; i++) originalBytes[i] = (byte)(i % 256);
            var data = DataList(originalBytes);

            packer.Pack(new DeliveryId(1), data,
                new ConsumerDelegate<QueuedMessage>(x => { sent.Add(x); return true; }));

            // Each QueuedMessage.Data is the wire message (packetId already prepended).
            // Element 0 = packetId(ushort), element 1 = type(byte), element 2 = id(ushort),
            // element 3 = partId(byte) or rpt for single, element 4 = partsNum for multi,
            // last element = payload(array).
            int totalPayload = 0;
            foreach (var qm in sent)
            {
                var elements = qm.Data.Elements;
                var payload = elements[elements.Count - 1].Bytes!;
                totalPayload += payload.Count;
            }

            // Total chunk payloads should equal the serialized user data size
            int serializedSize = 1 + 1 + ZigZagVarIntSerializer.GetIntEncodedSize(originalBytes.Length) + originalBytes.Length;
            Assert.That(totalPayload, Is.EqualTo(serializedSize));
            foreach (var qm in sent) qm.Data.Release();
        }

        // ── Boundary tests ──

        [Test]
        public void Pack_DataAtSingleChunkLimit_ProducesOneChunk()
        {
            var (packer, serializer, _) = CreatePackEnvironment();
            // 86 data bytes → serialized = 1(count) + 1(type) + 2(varint86) + 86 = 90 = singleMax
            var sent = new List<QueuedMessage>();

            var result = packer.Pack(new DeliveryId(1), DataList(new byte[86]),
                new ConsumerDelegate<QueuedMessage>(x => { sent.Add(x); return true; }));

            Assert.That(result, Is.EqualTo(SendResult.Ok));
            Assert.That(sent.Count, Is.EqualTo(1));
            sent[0].Data.Release();
        }

        [Test]
        public void Pack_DataJustOverSingleChunkLimit_ProducesMultipleChunks()
        {
            var (packer, serializer, _) = CreatePackEnvironment();
            // 87 data bytes → serialized = 1+1+2+87 = 91 > singleMax(90) → multi-chunk
            var sent = new List<QueuedMessage>();

            var result = packer.Pack(new DeliveryId(1), DataList(new byte[87]),
                new ConsumerDelegate<QueuedMessage>(x => { sent.Add(x); return true; }));

            Assert.That(result, Is.EqualTo(SendResult.Ok));
            Assert.That(sent.Count, Is.GreaterThan(1));
            foreach (var qm in sent) qm.Data.Release();
        }

        // ── Error cases ──

        [Test]
        public void Pack_DataTooBig_ReturnsMessageTooBig()
        {
            var (packer, _, _) = CreatePackEnvironment();
            var sent = new List<QueuedMessage>();

            var result = packer.Pack(new DeliveryId(1), DataList(new byte[30000]),
                new ConsumerDelegate<QueuedMessage>(x => { sent.Add(x); return true; }));

            Assert.That(result, Is.EqualTo(SendResult.MessageTooBig));
            Assert.That(sent, Is.Empty);
        }

        [Test]
        public void Pack_NullData_ReturnsInvalidMessage()
        {
            var (packer, _, _) = CreatePackEnvironment();
            var sent = new List<QueuedMessage>();

            var result = packer.Pack(new DeliveryId(1), null!,
                new ConsumerDelegate<QueuedMessage>(x => { sent.Add(x); return true; }));

            Assert.That(result, Is.EqualTo(SendResult.InvalidMessage));
            Assert.That(sent, Is.Empty);
        }

        // ── Clear ──

        [Test]
        public void Clear_DoesNotThrow()
        {
            var (packer, _, chunker) = CreatePackEnvironment();
            // Put something in the chunker's reassembly state
            var sent = new List<QueuedMessage>();
            packer.Pack(new DeliveryId(1), DataList(new byte[200]),
                new ConsumerDelegate<QueuedMessage>(x => { sent.Add(x); return true; }));
            foreach (var qm in sent) qm.Data.Release();

            Assert.DoesNotThrow(() => packer.Clear());
        }

        // ── Memory management ──

        [Test]
        public void Pack_ReleasesDataOnSuccess()
        {
            var (packer, _, _) = CreatePackEnvironment();
            var data = DataList(1, 2, 3);

            var result = packer.Pack(new DeliveryId(1), data,
                new ConsumerDelegate<QueuedMessage>(_ => true));

            Assert.That(result, Is.EqualTo(SendResult.Ok));
        }
    }
}
