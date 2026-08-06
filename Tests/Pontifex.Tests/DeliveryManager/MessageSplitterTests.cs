using Actuarius.Memory;

namespace Pontifex.Delivery.Tests
{
    [Category("DeliveryManager")]
    public class MessageSplitterTests
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
        public void GetChunkCount_ExactFit_ReturnsOne()
        {
            var splitter = new MessageSplitter(Pool, 5);
            Assert.That(splitter.GetChunkCount(5), Is.EqualTo(1));
        }

        [Test]
        public void GetChunkCount_LargerThanMax_ReturnsMultiple()
        {
            var splitter = new MessageSplitter(Pool, 3);
            Assert.That(splitter.GetChunkCount(7), Is.EqualTo(3));
        }

        [Test]
        public void GetChunkCount_Empty_ReturnsZero()
        {
            var splitter = new MessageSplitter(Pool, 10);
            Assert.That(splitter.GetChunkCount(0), Is.EqualTo(0));
        }

        [Test]
        public void GetNextChunk_IteratesAllChunks()
        {
            var splitter = new MessageSplitter(Pool, 3);
            var data = Data(1, 2, 3, 4, 5, 6, 7);

            int chunkId = 0;
            var all = new byte[7];
            while (splitter.GetNextChunk(data, chunkId, out var chunk))
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
            var splitter = new MessageSplitter(Pool, 5);
            var data = Data(1, 2, 3);

            Assert.That(splitter.GetNextChunk(data, 1, out _), Is.False);
            Assert.That(splitter.GetNextChunk(data, 5, out _), Is.False);
        }

        [Test]
        public void GetNextChunk_EmptyData_ReturnsFalse()
        {
            var splitter = new MessageSplitter(Pool, 10);
            var data = Data();

            Assert.That(splitter.GetNextChunk(data, 0, out _), Is.False);
        }
    }
}
