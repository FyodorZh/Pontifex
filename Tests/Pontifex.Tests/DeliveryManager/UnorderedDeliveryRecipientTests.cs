using Actuarius.Memory;

namespace Pontifex.DeliveryManager.Tests
{
    [Category("DeliveryManager")]
    public class UnorderedDeliveryRecipientTests
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
        public void Single_ReturnsSameData()
        {
            var recip = new UnorderedDeliveryRecipient(Pool);
            var data = Data(1, 2, 3);

            var result = recip.ReceivedSingle(data);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Count, Is.EqualTo(3));
            result.Release();
            data.Release();
        }

        [Test]
        public void Single_DoesNotReleaseInput()
        {
            var recip = new UnorderedDeliveryRecipient(Pool);
            var data = Data(42);
            data.AddRef();

            recip.ReceivedSingle(data);
            Assert.That(data.IsAlive, Is.True);
            data.Release();
            data.Release();
        }

        [Test]
        public void Multi_FirstChunk_ReturnsNull()
        {
            var recip = new UnorderedDeliveryRecipient(Pool);
            var result = recip.ReceivedMulti(new DeliveryId(1), 0, 2, Data(1));

            Assert.That(result, Is.Null);
        }

        [Test]
        public void Multi_AllChunksInOrder_ReturnsCombined()
        {
            var recip = new UnorderedDeliveryRecipient(Pool);
            var id = new DeliveryId(1);

            recip.ReceivedMulti(id, 0, 2, Data(10, 20));
            var result = recip.ReceivedMulti(id, 1, 2, Data(30, 40, 50));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(5));

            var bytes = new byte[5];
            result.CopyTo(bytes, 0, 0, 5);
            Assert.That(bytes, Is.EqualTo(new byte[] { 10, 20, 30, 40, 50 }));
            result.Release();
        }

        [Test]
        public void Multi_OutOfOrder_ReturnsCombined()
        {
            var recip = new UnorderedDeliveryRecipient(Pool);
            var id = new DeliveryId(2);

            recip.ReceivedMulti(id, 1, 2, Data(5, 6, 7));
            var result = recip.ReceivedMulti(id, 0, 2, Data(1, 2));

            Assert.That(result.Count, Is.EqualTo(5));

            var bytes = new byte[5];
            result.CopyTo(bytes, 0, 0, 5);
            Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 5, 6, 7 }));
            result.Release();
        }

        [Test]
        public void DuplicateChunk_Ignored()
        {
            var recip = new UnorderedDeliveryRecipient(Pool);
            var id = new DeliveryId(3);
            byte parts = 2;

            recip.ReceivedMulti(id, 0, parts, Data(1, 2));
            recip.ReceivedMulti(id, 0, parts, Data(99, 99));
            var result = recip.ReceivedMulti(id, 1, parts, Data(3, 4));

            var bytes = new byte[4];
            result.CopyTo(bytes, 0, 0, 4);
            Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            result.Release();
        }

        [Test]
        public void InvalidPartId_Ignored()
        {
            var recip = new UnorderedDeliveryRecipient(Pool);
            var id = new DeliveryId(4);

            var result = recip.ReceivedMulti(id, 5, 2, Data(1));
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Clear_WithPending_PreventsLaterCombine()
        {
            var recip = new UnorderedDeliveryRecipient(Pool);
            var id = new DeliveryId(5);

            recip.ReceivedMulti(id, 0, 2, Data(1));
            recip.Clear();
            var result = recip.ReceivedMulti(id, 1, 2, Data(2));

            Assert.That(result, Is.Null);
        }

        [Test]
        public void PartsNumber1_TreatedAsMulti()
        {
            var recip = new UnorderedDeliveryRecipient(Pool);
            var result = recip.ReceivedMulti(new DeliveryId(6), 0, 1, Data(42));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            result.Release();
        }

        [Test]
        public void DifferentIds_TrackedSeparately()
        {
            var recip = new UnorderedDeliveryRecipient(Pool);

            recip.ReceivedMulti(new DeliveryId(1), 0, 2, Data(1));
            recip.ReceivedMulti(new DeliveryId(2), 0, 2, Data(10));
            var r1 = recip.ReceivedMulti(new DeliveryId(1), 1, 2, Data(2));
            var r2 = recip.ReceivedMulti(new DeliveryId(2), 1, 2, Data(20));

            var b1 = new byte[2];
            r1.CopyTo(b1, 0, 0, 2);
            Assert.That(b1, Is.EqualTo(new byte[] { 1, 2 }));

            var b2 = new byte[2];
            r2.CopyTo(b2, 0, 0, 2);
            Assert.That(b2, Is.EqualTo(new byte[] { 10, 20 }));

            r1.Release();
            r2.Release();
        }
    }
}
