using System;
using Actuarius.Memory;
using NUnit.Framework;
using Shared;
using Shared.ByteSinks;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    public class TestIByteSink
    {
        [Test]
        public void TestByteArraySink()
        {
            byte[] bytes = new byte[7];
            var sink = ByteArraySink.ThreadInstance(bytes, 2);

            Test1_5(sink);

            try
            {
                sink.Put(0);
                Assert.Fail();
            }
            catch
            {
            }

            Assert.IsTrue(new ByteArraySegment(new byte[]{0, 0, 1, 2, 3, 4, 5}).EqualByContent(new ByteArraySegment(bytes)));

        }

        [Test]
        public void TestRangedByteArraySink()
        {
            byte[] bytes = new byte[5];
            var sink = RangedByteArraySink.ThreadInstance(1, bytes, 1, 3);

            Test1_5(sink);

            Assert.IsTrue(new ByteArraySegment(new byte[]{0, 2, 3, 4, 0}).EqualByContent(new ByteArraySegment(bytes)));
        }

        private void Test1_5(IByteSink sink)
        {
            try
            {
                sink.PutMany(new ByteArraySegment(new byte[0]));
                sink.Put(1);
                sink.Put(2);
                sink.PutMany(new ByteArraySegment(new byte[] {3, 4, 5}));
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
            }
        }
    }
}