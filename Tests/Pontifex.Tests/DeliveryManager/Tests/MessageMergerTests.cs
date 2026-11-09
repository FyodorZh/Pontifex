using Actuarius.Memory;
using Pontifex.Delivery;
using Pontifex.Tests.Delivery.Harness;

namespace Pontifex.Tests.Delivery
{
    [TestFixture]
    [Category("DeliveryManager")]
    public sealed class MessageMergerTests : TrackedFixture
    {
        private IMultiRefByteArray Data(params byte[] bytes)
        {
            var buf = Pool.Acquire(bytes.Length);
            Buffer.BlockCopy(bytes, 0, buf.Array, buf.Offset, bytes.Length);
            AddCleanup(buf.Release);
            return buf;
        }

        private MessageMerger NewMerger()
        {
            var merger = new MessageMerger(Pool);
            AddCleanup(merger.Clear);
            return merger;
        }

        [Test]
        public void Combine_FirstChunk_ReturnsNull()
        {
            var merger = NewMerger();
            var result = merger.Combine(new DeliveryId(1), 0, 2, Data(1));

            Assert.That(result, Is.Null);
        }

        [Test]
        public void Combine_AllChunksInOrder_ReturnsCombined()
        {
            var merger = NewMerger();
            var id = new DeliveryId(1);

            merger.Combine(id, 0, 2, Data(10, 20));
            var result = merger.Combine(id, 1, 2, Data(30, 40, 50));

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
            var merger = NewMerger();
            var id = new DeliveryId(2);

            merger.Combine(id, 1, 2, Data(5, 6, 7));
            var result = merger.Combine(id, 0, 2, Data(1, 2));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(5));

            var bytes = new byte[5];
            result.CopyTo(bytes, 0, 0, 5);
            Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 5, 6, 7 }));
            result.Release();
        }

        [Test]
        public void Combine_DuplicateChunk_Ignored()
        {
            var merger = NewMerger();
            var id = new DeliveryId(3);
            byte parts = 2;

            merger.Combine(id, 0, parts, Data(1, 2));
            merger.Combine(id, 0, parts, Data(99, 99));
            var result = merger.Combine(id, 1, parts, Data(3, 4));

            Assert.That(result, Is.Not.Null);
            var bytes = new byte[4];
            result!.CopyTo(bytes, 0, 0, 4);
            Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            result.Release();
        }

        [Test]
        public void Combine_InvalidPartId_Ignored()
        {
            var merger = NewMerger();
            var id = new DeliveryId(4);

            var result = merger.Combine(id, 5, 2, Data(1));
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Clear_WithPending_PreventsLaterCombine()
        {
            var merger = NewMerger();
            var id = new DeliveryId(5);

            merger.Combine(id, 0, 2, Data(1));
            merger.Clear();
            var result = merger.Combine(id, 1, 2, Data(2));

            Assert.That(result, Is.Null);
        }

        [Test]
        public void Combine_PartsNumber1_TreatedAsMulti()
        {
            var merger = NewMerger();
            var result = merger.Combine(new DeliveryId(6), 0, 1, Data(42));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            result.Release();
        }

        [Test]
        public void Combine_DifferentIds_TrackedSeparately()
        {
            var merger = NewMerger();

            merger.Combine(new DeliveryId(1), 0, 2, Data(1));
            merger.Combine(new DeliveryId(2), 0, 2, Data(10));
            var r1 = merger.Combine(new DeliveryId(1), 1, 2, Data(2));
            var r2 = merger.Combine(new DeliveryId(2), 1, 2, Data(20));
            Assert.That(r1, Is.Not.Null);
            Assert.That(r2, Is.Not.Null);

            var b1 = new byte[2];
            r1!.CopyTo(b1, 0, 0, 2);
            Assert.That(b1, Is.EqualTo(new byte[] { 1, 2 }));

            var b2 = new byte[2];
            r2!.CopyTo(b2, 0, 0, 2);
            Assert.That(b2, Is.EqualTo(new byte[] { 10, 20 }));

            r1.Release();
            r2.Release();
        }
    }
}
