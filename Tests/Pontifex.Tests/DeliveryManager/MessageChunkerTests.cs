using Actuarius.Memory;

namespace Pontifex.DeliveryManager.Tests
{
    [Category("DeliveryManager")]
    public class MessageChunkerTests
    {
        private static IMemoryRental Memory => MemoryRental.Shared;
        private static IPool<IMultiRefByteArray, int> Pool => Memory.ByteArraysPool;

        private static IMultiRefByteArray Data(params byte[] bytes)
        {
            var buf = Pool.Acquire(bytes.Length);
            Buffer.BlockCopy(bytes, 0, buf.Array, buf.Offset, bytes.Length);
            return buf;
        }

        [Test]
        public void Combine_FirstChunk_ReturnsNull()
        {
            var chunker = new MessageChunker(Pool, 10);
            var result = chunker.Combine(new DeliveryId(1), 0, 2, Data(1));

            Assert.That(result, Is.Null);
        }

        [Test]
        public void Combine_AllChunksInOrder_ReturnsCombined()
        {
            var chunker = new MessageChunker(Pool, 10);
            var id = new DeliveryId(1);

            chunker.Combine(id, 0, 2, Data(10, 20));
            var result = chunker.Combine(id, 1, 2, Data(30, 40, 50));

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
            var chunker = new MessageChunker(Pool, 10);
            var id = new DeliveryId(2);

            chunker.Combine(id, 1, 2, Data(5, 6, 7));
            var result = chunker.Combine(id, 0, 2, Data(1, 2));

            Assert.That(result.Count, Is.EqualTo(5));

            var bytes = new byte[5];
            result.CopyTo(bytes, 0, 0, 5);
            Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 5, 6, 7 }));
            result.Release();
        }

        [Test]
        public void Combine_DuplicateChunk_Ignored()
        {
            var chunker = new MessageChunker(Pool, 10);
            var id = new DeliveryId(3);
            byte parts = 2;

            chunker.Combine(id, 0, parts, Data(1, 2));
            chunker.Combine(id, 0, parts, Data(99, 99));
            var result = chunker.Combine(id, 1, parts, Data(3, 4));

            var bytes = new byte[4];
            result.CopyTo(bytes, 0, 0, 4);
            Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            result.Release();
        }

        [Test]
        public void Combine_InvalidPartId_Ignored()
        {
            var chunker = new MessageChunker(Pool, 10);
            var id = new DeliveryId(4);

            var result = chunker.Combine(id, 5, 2, Data(1));
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Clear_WithPending_PreventsLaterCombine()
        {
            var chunker = new MessageChunker(Pool, 10);
            var id = new DeliveryId(5);

            chunker.Combine(id, 0, 2, Data(1));
            chunker.Clear();
            var result = chunker.Combine(id, 1, 2, Data(2));

            Assert.That(result, Is.Null);
        }

        [Test]
        public void Combine_PartsNumber1_TreatedAsMulti()
        {
            var chunker = new MessageChunker(Pool, 10);
            var result = chunker.Combine(new DeliveryId(6), 0, 1, Data(42));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            result.Release();
        }

        [Test]
        public void Combine_DifferentIds_TrackedSeparately()
        {
            var chunker = new MessageChunker(Pool, 10);

            chunker.Combine(new DeliveryId(1), 0, 2, Data(1));
            chunker.Combine(new DeliveryId(2), 0, 2, Data(10));
            var r1 = chunker.Combine(new DeliveryId(1), 1, 2, Data(2));
            var r2 = chunker.Combine(new DeliveryId(2), 1, 2, Data(20));

            var b1 = new byte[2];
            r1.CopyTo(b1, 0, 0, 2);
            Assert.That(b1, Is.EqualTo(new byte[] { 1, 2 }));

            var b2 = new byte[2];
            r2.CopyTo(b2, 0, 0, 2);
            Assert.That(b2, Is.EqualTo(new byte[] { 10, 20 }));

            r1.Release();
            r2.Release();
        }

        [Test]
        public void GetChunkCount_ExactFit_ReturnsOne()
        {
            var chunker = new MessageChunker(Pool, 5);
            Assert.That(chunker.GetChunkCount(5), Is.EqualTo(1));
        }

        [Test]
        public void GetChunkCount_LargerThanMax_ReturnsMultiple()
        {
            var chunker = new MessageChunker(Pool, 3);
            Assert.That(chunker.GetChunkCount(7), Is.EqualTo(3));
        }

        [Test]
        public void GetChunkCount_Empty_ReturnsZero()
        {
            var chunker = new MessageChunker(Pool, 10);
            Assert.That(chunker.GetChunkCount(0), Is.EqualTo(0));
        }

        [Test]
        public void GetNextChunk_IteratesAllChunks()
        {
            var chunker = new MessageChunker(Pool, 3);
            var data = Data(1, 2, 3, 4, 5, 6, 7);

            int chunkId = 0;
            var all = new byte[7];
            while (chunker.GetNextChunk(data, chunkId, out var chunk))
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
            var chunker = new MessageChunker(Pool, 5);
            var data = Data(1, 2, 3);

            Assert.That(chunker.GetNextChunk(data, 1, out _), Is.False);
            Assert.That(chunker.GetNextChunk(data, 5, out _), Is.False);
        }

        [Test]
        public void GetNextChunk_EmptyData_ReturnsFalse()
        {
            var chunker = new MessageChunker(Pool, 10);
            var data = Data();

            Assert.That(chunker.GetNextChunk(data, 0, out _), Is.False);
        }
    }
}
