using System;
using System.Collections.Generic;
using NUnit.Framework;
using Transport.Protocols.Reliable.Delivery;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    public class DeduplicatorTest
    {
        [Test]
        public void Test()
        {
            int N = 1024 * 1024;

            Deduplicator deduplicator = new Deduplicator(100);
            bool[] checker = new bool[N + 1];

            Random rnd = new Random(777);

            List<uint> test = new List<uint>();
            for (int i = 1; i <= N; ++i)
            {
                for (int j = 0; j < 10; ++j)
                {
                    int id = i + rnd.Next() % 11 - 5;
                    test.Add((uint)Math.Max(1, Math.Min(id, N)));
                }
                test.Add((uint)i);
            }

            for (int i = 0; i < test.Count; ++i)
            {
                uint id = test[i];
                switch (deduplicator.Received(id))
                {
                    case Deduplicator.Result.New:
                        Assert.IsFalse(checker[id], "Error1");
                        checker[id] = true;
                        break;
                    case Deduplicator.Result.Duplicate:
                        // DO NOTHING
                        break;
                    case Deduplicator.Result.Overflow:
                        Assert.Fail("Overflow");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            for (uint i = 1; i <= N; ++i)
            {
                Assert.IsTrue(checker[i], "Error2");
            }
        }
    }
}