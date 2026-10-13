using System.Net.Sockets;
using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Strict SPSC double: overlapping readers/writers are recorded instead of silently
    /// serialized, while one reader plus one writer stays legal.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class ControllableQueueTests
    {
        [Test]
        public void StrictSource_RejectsOverlappingReaders()
        {
            var queue = new ControllableQueue { StrictMode = true };
            queue.Pause();

            var firstEntered = new ManualResetEventSlim(false);
            var secondEntered = new ManualResetEventSlim(false);
            int entered = 0;
            queue.OnPopEntered = () =>
            {
                if (Interlocked.Increment(ref entered) == 1)
                {
                    firstEntered.Set();
                }
                else
                {
                    secondEntered.Set();
                }
            };

            var first = new BackgroundAction(() => queue.TryPop(out _), "stage1-reader-1");
            var second = new BackgroundAction(() => queue.TryPop(out _), "stage1-reader-2");
            try
            {
                Assert.That(firstEntered.Wait(TestWait.DefaultTimeout), Is.True);
                Assert.That(secondEntered.Wait(TestWait.DefaultTimeout), Is.True);

                Assert.That(queue.ReaderOverlapCount, Is.EqualTo(1),
                    "a second concurrent reader must be reported, not silently serialized");
                Assert.That(queue.WriterOverlapCount, Is.EqualTo(0));
            }
            finally
            {
                queue.Resume();
                first.Join();
                second.Join();
            }
        }

        [Test]
        public void StrictSource_RejectsOverlappingWriters()
        {
            var queue = new ControllableQueue { StrictMode = true };
            queue.PausePuts();

            var firstEntered = new ManualResetEventSlim(false);
            var secondEntered = new ManualResetEventSlim(false);
            int entered = 0;
            queue.OnPutEntered = () =>
            {
                if (Interlocked.Increment(ref entered) == 1)
                {
                    firstEntered.Set();
                }
                else
                {
                    secondEntered.Set();
                }
            };

            var first = new BackgroundAction(() => queue.Put(TestBuffers.Of(1)), "stage1-writer-1");
            var second = new BackgroundAction(() => queue.Put(TestBuffers.Of(2)), "stage1-writer-2");
            try
            {
                Assert.That(firstEntered.Wait(TestWait.DefaultTimeout), Is.True);
                Assert.That(secondEntered.Wait(TestWait.DefaultTimeout), Is.True);

                Assert.That(queue.WriterOverlapCount, Is.EqualTo(1),
                    "a second concurrent writer must be reported, not silently serialized");
                Assert.That(queue.ReaderOverlapCount, Is.EqualTo(0));
            }
            finally
            {
                queue.ResumePuts();
                first.Join();
                second.Join();
            }

            Assert.That(queue.Count, Is.EqualTo(2));
        }

        [Test]
        public void StrictSource_AllowsSingleReaderAndSingleWriter()
        {
            var queue = new ControllableQueue { StrictMode = true };
            queue.Pause();

            var readerEntered = new ManualResetEventSlim(false);
            queue.OnPopEntered = () => readerEntered.Set();
            var item = TestBuffers.Filled(4, 7);

            var reader = new BackgroundAction(() =>
            {
                Assert.That(queue.TryPop(out IMultiRefByteArray popped), Is.True);
                Assert.That(popped, Is.SameAs(item));
            }, "stage1-reader");

            try
            {
                Assert.That(readerEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the reader must be inside TryPop before the writer publishes");
                Assert.That(queue.Put(item), Is.True);
            }
            finally
            {
                queue.Resume();
                reader.Join();
            }

            Assert.That(queue.ReaderOverlapCount, Is.EqualTo(0), "one reader plus one writer is legal");
            Assert.That(queue.WriterOverlapCount, Is.EqualTo(0), "one reader plus one writer is legal");
        }
    }
}
