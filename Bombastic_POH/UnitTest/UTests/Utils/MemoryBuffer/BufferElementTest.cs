using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shared;
using Shared.Buffer;
using Shared.ByteSinks;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    class BufferElementTest
    {
        public BufferElementTest()
        {
        }

        private void TestBool(params bool[] list)
        {
            for (int i = 0; i < list.Length; ++i)
            {
                BufferElement el = new BufferElement(list[i]);
                Assert.AreEqual(1, el.Size);
                Assert.AreEqual(BufferElementType.Bool, el.Type);
                Assert.AreEqual(true, el.AsBoolean(out var val));
                Assert.AreEqual(list[i], val);
                Check(new BufferElement(list[i]));
            }
        }

        private void TestByte(params byte[] list)
        {
            for (int i = 0; i < list.Length; ++i)
            {
                BufferElement el = new BufferElement(list[i]);
                Assert.AreEqual("Byte: " + list[i], el.ToString());
                Assert.AreEqual(list[i] < 15 ? 1 : 2, el.Size);
                Assert.AreEqual(BufferElementType.Byte, el.Type);
                Assert.AreEqual(true, el.AsByte(out var val));
                Assert.AreEqual(list[i], val);
                Check(new BufferElement(list[i]));
            }
        }

        private void TestUInt16(params UInt16[] list)
        {
            for (int i = 0; i < list.Length; ++i)
            {
                BufferElement el = new BufferElement(list[i]);
                Assert.AreEqual("UInt16: " + list[i], el.ToString());
                int size;
                if (list[i] < 15 - 2)
                    size = 1;
                else if (list[i] < 256)
                    size = 2;
                else
                    size = 3;
                Assert.AreEqual(size, el.Size);
                Assert.AreEqual(BufferElementType.UInt16, el.Type);
                Assert.AreEqual(true, el.AsUInt16(out var val));
                Assert.AreEqual(list[i], val);
                Check(new BufferElement(list[i]));
            }
        }

        private void TestInt32(params Int32[] list)
        {
            for (int i = 0; i < list.Length; ++i)
            {
                BufferElement el = new BufferElement(list[i]);
                Assert.AreEqual("Int32: " + list[i], el.ToString());
                int size;
                if (list[i] == 0 || list[i] == 1 || list[i] == -1)
                    size = 1;
                else if (list[i] == (sbyte)list[i])
                    size = 2;
                else if (list[i] == (short)list[i])
                    size = 3;
                else
                    size = 5;
                Assert.AreEqual(size, el.Size, "Value = " + list[i]);
                Assert.AreEqual(BufferElementType.Int32, el.Type);
                Assert.AreEqual(true, el.AsInt32(out var val));
                Assert.AreEqual(list[i], val);
                Check(new BufferElement(list[i]));
            }
        }

        private void TestAbstractArray(params IMultiRefByteArray[] list)
        {
            for (int i = 0; i < list.Length; ++i)
            {
                int len = list[i].Count;

                BufferElement el = new BufferElement(list[i]);
                //Assert.AreEqual("AbstractArray: " + list[i], el.ToString());

                int size;
                if (len < 1)
                    size = 1;
                else if (len < 15 - 1)
                    size = 1 + len;
                else
                {
                    size = 1 + 3 + len;
                }
                Assert.AreEqual(size, el.Size, "Value = " + list[i]);
                Assert.AreEqual(BufferElementType.AbstractArray, el.Type);
                Assert.AreEqual(true, el.AsAbstractArray(out var val));
                Assert.IsTrue(list[i].EqualByContent(val));
                Check(new BufferElement(list[i]));

                val.Release();
                list[i].Release();
            }
        }

        private void TestArray(ByteArraySegment array, int byteSize)
        {
            BufferElement el = new BufferElement(array);

            Assert.AreEqual(byteSize, el.Size);

            Assert.IsTrue(el.AsArray(out var chkList));
            Assert.IsTrue(chkList.EqualByContent(array));

            Check(new BufferElement(array));
        }

        [Test]
        public void Test()
        {
            {
                BufferElement el = new BufferElement();
                Assert.AreEqual(el.Size, 1);
                Assert.AreEqual(el.Type, BufferElementType.Unknown);
                Check(el);
            }

            TestBool(true, false);

            TestByte(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 16, 20, 31, 32, 127, 128, 255);
//            for (int i = 0; i <= Byte.MaxValue; ++i)
//            {
//                TestByte((byte)i);
//            }

            TestUInt16(0, 1, 2, 3, 7, 127, 128, 255, 256, 1000, 10000, UInt16.MaxValue);
//            for (int i = 0; i <= UInt16.MaxValue; ++i)
//            {
//                TestUInt16((ushort)i);
//            }

            TestInt32(0, 1, 2, 7, sbyte.MaxValue, sbyte.MaxValue + 1, byte.MaxValue, byte.MaxValue + 1, short.MaxValue, short.MaxValue + 1, int.MaxValue);
            TestInt32(0, -1, -2, -7, sbyte.MinValue, sbyte.MinValue - 1, short.MinValue, short.MinValue - 1, int.MinValue);
//            for (int i = -100000; i < 100000; ++i)
//            {
//                TestInt32(i);
//            }

            {
                BufferElement el = new BufferElement(123123123123L);
                Assert.AreEqual(9, el.Size);
                Assert.AreEqual(BufferElementType.Int64, el.Type);
                Assert.AreEqual(true, el.AsInt64(out var val));
                Assert.AreEqual(123123123123L, val);
                Check(new BufferElement(123123123123L));
            }

            {
                BufferElement el = new BufferElement(10.777f);
                Assert.AreEqual(5, el.Size);
                Assert.AreEqual(BufferElementType.Single, el.Type);
                Assert.AreEqual(true, el.AsSingle(out var val));
                Assert.AreEqual(10.777f, val);
                Check(new BufferElement(10.777f));
            }

            {
                BufferElement el = new BufferElement(10.777);
                Assert.AreEqual(9, el.Size);
                Assert.AreEqual(BufferElementType.Double, el.Type);
                Assert.AreEqual(true, el.AsDouble(out var val));
                Assert.AreEqual(10.777, val);
                Check(new BufferElement(10.777));
            }

            {
                byte[] bytes = new byte[100];
                for (int i = 0; i < bytes.Length; ++i)
                {
                    bytes[i] = (byte)i;
                }

                List<IMultiRefByteArray> list = new List<IMultiRefByteArray>();
                list.Add(new ByteArraySegment());
                list.Add(new ByteArraySegment(new byte[0]));
                for (int len = 1; len < 40; ++len)
                {
                    list.Add(new ByteArraySegment(bytes, len, len));
                }

                TestAbstractArray(list.ToArray());
            }

            {
                BufferElement el;
                using (var bufferAccessor = ConcurrentUsageMemoryBufferPool.Instance.Allocate().ExposeAccessorOnce())
                {
                    bufferAccessor.Buffer.PushByte(2);
                    bufferAccessor.Buffer.PushDouble(3);

                    using (var bufferAccessor2 = ConcurrentUsageMemoryBufferPool.Instance.Allocate().ExposeAccessorOnce())
                    {
                        bufferAccessor2.Buffer.PushBoolean(true);
                        bufferAccessor.Buffer.PushBuffer(bufferAccessor2.Acquire());
                    }

                    el = new BufferElement(bufferAccessor.Acquire());
                }

                Assert.AreEqual(BufferElementType.Buffer, el.Type);
                //Assert.AreEqual((((1) + 3 + 1) + (8 + 1) + (1 + 1)) + 3 + 1, el.Size);

                Check(el);
            }


            {
                TestArray(new ByteArraySegment(), 1);
                TestArray(new ByteArraySegment(new byte[]{ }), 1);

                int maxLen = 1000;
                byte[] bytes = new byte[maxLen + 1];
                new Random(123).NextBytes(bytes);

                for (int len = 1; len < 1000; ++len)
                {
                    int size;
                    if (len < 1)
                        size = 1;
                    else if (len < 15 - 1)
                        size = 1 + len;
                    else
                    {
                        size = 1 + 3 + len;
                    }
                    TestArray(new ByteArraySegment(bytes, 1, len), size);
                }
            }
        }

        private static void Check(BufferElement el)
        {
            byte[] data = new byte[el.Size];
            var sink = ByteArraySink.ThreadInstance(data);
            Assert.IsTrue(el.TryWriteTo(sink));

            int pos = 0;
            BufferElement newEl = new BufferElement(data, ref pos, null);

            Assert.AreEqual(el.Size, newEl.Size);

            Assert.AreEqual(el.Type, newEl.Type);

            switch (el.Type)
            {
                case BufferElementType.Unknown:
                    break;
                case BufferElementType.Byte:
                    {
                        el.AsByte(out var v1);
                        newEl.AsByte(out var v2);
                        Assert.AreEqual(v1, v2);
                        break;
                    }
                case BufferElementType.UInt16:
                    {
                        el.AsUInt16(out var v1);
                        newEl.AsUInt16(out var v2);
                        Assert.AreEqual(v1, v2);
                        break;
                    }
                case BufferElementType.Int32:
                    {
                        el.AsInt32(out var v1);
                        newEl.AsInt32(out var v2);
                        Assert.AreEqual(v1, v2);
                        break;
                    }
                case BufferElementType.Int64:
                    {
                        el.AsInt64(out var v1);
                        newEl.AsInt64(out var v2);
                        Assert.AreEqual(v1, v2);
                        break;
                    }
                case BufferElementType.Array:
                    {
                        el.AsArray(out var v1);
                        newEl.AsArray(out var v2);
                        Assert.IsTrue(v1.EqualByContent(v2));
                        break;
                    }
                case BufferElementType.AbstractArray:
                    {
                        el.AsAbstractArray(out var v1);
                        newEl.AsAbstractArray(out var v2);
                        Assert.IsTrue(v2.EqualByContent(v1));
                        v1.Release();
                        v2.Release();
                        break;
                    }
            }

            el.Clear();
            newEl.Clear();
        }
    }
}
