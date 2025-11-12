using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shared;
using Shared.Buffer;
using Transport.Transports.Tcp;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    public class PacketCompositorTest
    {
        [Test]
        public void Test()
        {
            Random rnd = new Random(123);

            byte[] buffer = new byte[1024];
            for (int i = 0; i < buffer.Length; ++i)
            {
                buffer[i] = (byte)(i % 256);
            }

            List<Packet> list = new List<Packet>();
            for (int i = 0; i <= 1024; ++i)
            {
                var bufferHolder = ConcurrentUsageMemoryBufferPool.Instance.Allocate();
                using (var bufferAccessor = bufferHolder.Acquire().ExposeAccessorOnce())
                {
                    bufferAccessor.Buffer.PushArray(new ByteArraySegment(buffer, 0, i));
                    Packet packet = new Packet((PacketType)(i % 256), bufferAccessor.Acquire());
                    list.Add(packet);
                }
            }

            PacketCompositor compositor = new PacketCompositor(1024 + 5);

            System.IO.MemoryStream stream = new System.IO.MemoryStream();
            for (int i = 0; i < list.Count; ++i)
            {
                byte[] data = new byte[0];
                int size = PacketCompositor.EncodePacketTo(list[i], ref data);
                stream.Write(data, 0, size);
            }

            byte[] mem = stream.ToArray();

            int id = -1;
            int pos = 0;
            while (pos < mem.Length)
            {
                int dPos = Math.Min(rnd.Next(100) + 1, mem.Length - pos);
                byte[] msg = new ByteArraySegment(mem, pos, dPos).Clone();
                pos += dPos;
                compositor.DecodePackets(msg, 0, msg.Length, (packet) =>
                    {
                        ++id;
                        Assert.AreEqual(list[id].Type, packet.Type, "Type mismatch");
                        using (var bufferAccessor = packet.Buffer.ExposeAccessorOnce())
                        {
                            Assert.IsTrue(bufferAccessor.Buffer.PopFirst().AsArray(out var data1));
                            using (var original = list[id].Buffer.ExposeAccessorOnce())
                            {
                                Assert.IsTrue(original.Buffer.PopFirst().AsArray(out var data2));
                                Assert.IsTrue(data2.EqualByContent(data1), "Data mismatch");
                            }
                        }
                    });
            }

            compositor.Destroy();
        }
    }
}