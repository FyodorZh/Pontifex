using System;
using NUnit.Framework;
using Shared;
using Shared.Buffer;
using Shared.ByteSinks;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    class MemoryBufferTest
    {
        public MemoryBufferTest()
        {
        }

        [Test]
        public void Test()
        {
            IMemoryBuffer buffer = new MemoryBuffer();

            bool b1 = true;
            byte v1 = 4;
            ushort v2 = 11323;
            int v3 = int.MinValue;
            Int64 v4 = Int64.MaxValue;
            Int64 v5 = Int64.MinValue;
            float v6 = 123.23f;
            double v7 = 123.23;

            buffer.PushBoolean(b1, false);
            buffer.PushByte(v1, false);
            buffer.PushUInt16(v2, false);
            buffer.PushInt32(v3, false);
            buffer.PushInt64(v4, false);
            buffer.PushInt64(v5, false);
            buffer.PushSingle(v6, false);
            buffer.PushDouble(v7, false);


            byte[] data = buffer.ToArray();
            Assert.IsTrue(data != null);

            MemoryBuffer buffer2 = new MemoryBuffer();
            Assert.IsTrue(buffer2.ReadFrom(new ByteArraySegment(data)));


            Assert.AreEqual(true, buffer2.PopFirst().AsBoolean(out var _b1));
            Assert.AreEqual(true, buffer2.PopFirst().AsByte  (out var _v1));
            Assert.AreEqual(true, buffer2.PopFirst().AsUInt16(out var _v2));
            Assert.AreEqual(true, buffer2.PopFirst().AsInt32 (out var _v3));
            Assert.AreEqual(true, buffer2.PopFirst().AsInt64 (out var _v4));
            Assert.AreEqual(true, buffer2.PopFirst().AsInt64 (out var _v5));
            Assert.AreEqual(true, buffer2.PopFirst().AsSingle(out var _v6));
            Assert.AreEqual(true, buffer2.PopFirst().AsDouble(out var _v7));

            Assert.AreEqual(b1, _b1);
            Assert.AreEqual(v1, _v1);
            Assert.AreEqual(v2, _v2);
            Assert.AreEqual(v3, _v3);
            Assert.AreEqual(v4, _v4);
            Assert.AreEqual(v5, _v5);
            Assert.AreEqual(v6, _v6);
            Assert.AreEqual(v7, _v7);
            Assert.AreEqual(0, buffer2.Count);
        }

        [Test]
        public void TestClone()
        {
            var root = ConcurrentUsageMemoryBufferPool.Instance.Allocate();
            using (var rootAccessor = root.Acquire().ExposeAccessorOnce())
            {
                rootAccessor.Buffer.PushAbstractArray(new ByteArraySegment(new byte[]{1, 2, 3}));
                rootAccessor.Buffer.PushByte(0);

                var subA = ConcurrentUsageMemoryBufferPool.Instance.Allocate();
                using (var a = subA.Acquire().ExposeAccessorOnce())
                {
                    a.Buffer.PushByte(1);
                }
                rootAccessor.Buffer.PushBuffer(subA);

                var subB = ConcurrentUsageMemoryBufferPool.Instance.Allocate();
                using (var b = subB.Acquire().ExposeAccessorOnce())
                {
                    b.Buffer.PushByte(2);
                }
                rootAccessor.Buffer.PushBuffer(subB);
            }

            Assert.IsTrue(ConcurrentUsageMemoryBufferPool.Instance.AllocateAndClone(root, out var clone));

            byte[] cloneBytes;
            using (var a = clone.ExposeAccessorOnce())
            {
                cloneBytes = new byte[a.Buffer.Size];
                var sink = ByteArraySink.ThreadInstance(cloneBytes);
                a.Buffer.TryWriteTo(sink);
            }

            byte[] origBytes;
            using (var a = root.ExposeAccessorOnce())
            {
                origBytes = new byte[a.Buffer.Size];
                var sink = ByteArraySink.ThreadInstance(origBytes);
                a.Buffer.TryWriteTo(sink);
            }

            ByteArraySegment bytes1 = new ByteArraySegment(origBytes);
            ByteArraySegment bytes2 = new ByteArraySegment(cloneBytes);

            Assert.IsTrue(bytes1.EqualByContent(bytes2));

            Assert.IsTrue(ConcurrentUsageMemoryBufferPool.Instance.AllocateAndDeserialize(bytes1, out var deserialized));

            byte val;
            IMemoryBufferHolder sub;
            using (var a0 = deserialized.ExposeAccessorOnce())
            {
                Assert.IsTrue(a0.Buffer.PopFirst().AsBuffer(out sub));
                using (var a1 = sub.ExposeAccessorOnce())
                {
                    Assert.IsTrue(a1.Buffer.PopFirst().AsByte(out val));
                    Assert.AreEqual(2, val);
                }

                Assert.IsTrue(a0.Buffer.PopFirst().AsBuffer(out sub));
                using (var a1 = sub.ExposeAccessorOnce())
                {
                    Assert.IsTrue(a1.Buffer.PopFirst().AsByte(out val));
                    Assert.AreEqual(1, val);
                }

                Assert.IsTrue(a0.Buffer.PopFirst().AsByte(out val));
                Assert.AreEqual(0, val);

                IMultiRefByteArray bytes;
                Assert.IsTrue(a0.Buffer.PopFirst().AsAbstractArray(out bytes));
                Assert.IsTrue(new ByteArraySegment(new byte[]{1, 2, 3}).EqualByContent(bytes));
                bytes.Release();
            }


            GC.Collect(2, GCCollectionMode.Forced);
        }
    }
}
