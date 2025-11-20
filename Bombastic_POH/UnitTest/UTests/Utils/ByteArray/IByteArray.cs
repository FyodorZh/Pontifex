using System;
using Actuarius.Memory;
using NUnit.Framework;
using Shared;
using Shared.Buffer;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    public class TestIByteArray
    {
        [Test]
        public void TestByteArraySegment()
        {
            Test0_5(new ByteArraySegment(new byte[]{0, 1, 2 ,3, 4, 5}), new byte[]{0, 1, 2, 3, 4, 5});
        }

        [Test]
        public void TestCollectableAbstractByteArraySegment()
        {
            var segment = ConcurrentPools.Acquire<CollectableAbstractByteArraySegment>().Init(
                new ByteArraySegment(new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8}), 2, 5);

            Test0_5(segment, new byte[]{2, 3, 4, 5, 6});

            segment.Release();
        }

        [Test]
        public void TestCollectableMultiSegmentByteArray()
        {
            var segment = ConcurrentPools.Acquire<CollectableMultiSegmentByteArray>().Init(new IMultiRefByteArray[] {
                    new ByteArraySegment(new byte[]{1, 2 ,3 }),
                    new ByteArraySegment(new byte[]{ }),
                    new ByteArraySegment(new byte[]{4}),
                    new ByteArraySegment(new byte[]{5, 6 })
            });

            Test0_5(segment, new byte[]{1, 2, 3, 4, 5, 6});

            segment.Release();
        }

        [Test]
        public void TestMemoryBufferHolder()
        {
            using (var bufferAccessor = ConcurrentUsageMemoryBufferPool.Instance.Allocate().ExposeAccessorOnce())
            {
                bufferAccessor.Buffer.PushInt32(1234512345);
                bufferAccessor.Buffer.PushArray(new ByteArraySegment(new byte[]{1, 2}));

                using (var sub = ConcurrentUsageMemoryBufferPool.Instance.Allocate().ExposeAccessorOnce())
                {
                    sub.Buffer.PushAbstractArray(new ByteArraySegment(new byte[] {7, 8}));
                    bufferAccessor.Buffer.PushBuffer(sub.Acquire());
                }

                var holder = bufferAccessor.Acquire();
                byte[] bytes = holder.ToRawArray();

                Test0_5(holder, bytes);
                holder.Release();
            }
        }

        private void Test0_5(IReadOnlyBytes array, byte[] bytes)
        {
            Assert.IsTrue(array.IsValid);
            Assert.AreEqual(bytes.Length, array.Count);
            Assert.IsTrue(array.EqualByContent(new ByteArraySegment(bytes)));

            int cnt = 0;

            System.Random rnd = new Random(123);
            for (int i = 0; i < 10000; ++i)
            {
                byte[] dst1 = new byte[bytes.Length * 2];
                byte[] dst2 = new byte[bytes.Length * 2];

                int dstOffset = rnd.Next(bytes.Length + 3) - 3;
                int srcOffset = rnd.Next(bytes.Length + 3) - 3;
                int count = rnd.Next(bytes.Length + 3) - 3;


                bool res1;
                try
                {
                    Buffer.BlockCopy(bytes, srcOffset, dst1, dstOffset, count);
                    res1 = true;
                }
                catch
                {
                    res1 = false;
                }

                bool res2 = array.CopyTo(dst2, dstOffset, srcOffset, count);

                if (res1 != res2)
                {
                    res2 = array.CopyTo(dst2, dstOffset, srcOffset, count);
                }

                Assert.AreEqual(res1, res2);
                if (res1)
                {
                    ++cnt;
                    Assert.IsTrue(new ByteArraySegment(dst1).EqualByContent(new ByteArraySegment(dst2)));
                }
            }

            Console.WriteLine("Count = " + cnt);
        }
    }
}