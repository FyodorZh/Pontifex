using System.Collections.Concurrent;
using System.Threading;
using Pontifex.Utils;

namespace Pontifex.Tests
{
    public class SerializedCallbackQueueTests
    {
        private const int TimeoutMs = 5000;

        [Test]
        public void Handler_RunsOnDedicatedBackgroundThread_WithGivenName()
        {
            string? threadName = null;
            bool isBackground = false;
            var started = new ManualResetEventSlim();

            using (var queue = new SerializedCallbackQueue<int>(
                       10,
                       "test-queue",
                       _ =>
                       {
                           threadName = Thread.CurrentThread.Name;
                           isBackground = Thread.CurrentThread.IsBackground;
                           started.Set();
                       },
                       _ => { }))
            {
                Assert.That(queue.Post(42), Is.True);

                Assert.That(started.Wait(TimeoutMs), Is.True, "handler was not invoked in time");
            }

            Assert.That(threadName, Is.EqualTo("test-queue"));
            Assert.That(isBackground, Is.True);
        }

        [Test]
        public void Posts_AreProcessedInFifoOrder()
        {
            const int count = 50;
            var processed = new ConcurrentQueue<int>();
            var remaining = new CountdownEvent(count);

            using (var queue = new SerializedCallbackQueue<int>(
                       100,
                       "fifo",
                       item =>
                       {
                           processed.Enqueue(item);
                           remaining.Signal();
                       },
                       _ => { }))
            {
                for (int i = 0; i < count; i++)
                {
                    Assert.That(queue.Post(i), Is.True, $"Post({i}) should be accepted");
                }

                Assert.That(remaining.Wait(TimeoutMs), Is.True, "not all items were processed in time");
            }

            Assert.That(processed.ToArray(), Is.EqualTo(Enumerable.Range(0, count).ToArray()));
        }

        [Test]
        public void Post_WhenCapacityExceeded_ReturnsFalse()
        {
            const int capacity = 3;
            var gate = new ManualResetEventSlim(true);
            var firstInvoked = new ManualResetEventSlim();
            var handled = 0;
            var entered = new CountdownEvent(capacity + 1);

            gate.Reset();

            using (var queue = new SerializedCallbackQueue<int>(
                       capacity,
                       "bounded",
                       item =>
                       {
                           Interlocked.Increment(ref handled);
                           entered.Signal();
                           if (item == 0)
                               firstInvoked.Set();
                           gate.Wait();
                       },
                       _ => { }))
            {
                Assert.That(queue.Post(0), Is.True);
                Assert.That(firstInvoked.Wait(TimeoutMs), Is.True, "worker did not pick up the first item");

                for (int i = 1; i <= capacity; i++)
                {
                    Assert.That(queue.Post(i), Is.True, $"Post({i}) should fit into the queue");
                }

                Assert.That(queue.Post(capacity + 1), Is.False, "queue is full, Post must report overflow");

                gate.Set();

                Assert.That(entered.Wait(TimeoutMs), Is.True, "queued items were not processed after release");
            }

            Assert.That(Interlocked.CompareExchange(ref handled, 0, 0), Is.EqualTo(capacity + 1));
        }

        [Test]
        public void HandlerException_IsRoutedToExceptionHandler_AndQueueKeepsProcessing()
        {
            var exceptions = new ConcurrentQueue<Exception>();
            var handled = 0;
            var remaining = new CountdownEvent(5);

            using (var queue = new SerializedCallbackQueue<int>(
                       10,
                       "exceptions",
                       item =>
                       {
                           Interlocked.Increment(ref handled);
                           remaining.Signal();
                           if (item == 2)
                               throw new InvalidOperationException("boom");
                       },
                       _ => { }))
            {
                queue.ExceptionHandler += ex => exceptions.Enqueue(ex);

                for (int i = 0; i < 5; i++)
                {
                    Assert.That(queue.Post(i), Is.True);
                }

                Assert.That(remaining.Wait(TimeoutMs), Is.True, "queue stopped after a handler exception");
            }

            Assert.That(Interlocked.CompareExchange(ref handled, 0, 0), Is.EqualTo(5));
            Assert.That(exceptions, Has.Count.EqualTo(1));
            Assert.That(exceptions.Single().Message, Is.EqualTo("boom"));
        }

        [Test]
        public void Dispose_ReleasesQueuedItemsViaDisposer_InFifoOrder()
        {
            const int posted = 5;
            var gate = new ManualResetEventSlim(true);
            var firstInvoked = new ManualResetEventSlim();
            var handled = 0;
            var disposed = new List<int>();
            var disposerLock = new object();

            gate.Reset();

            using (var queue = new SerializedCallbackQueue<int>(
                       10,
                       "dispose",
                       item =>
                       {
                           Interlocked.Increment(ref handled);
                           if (item == 0)
                               firstInvoked.Set();
                           gate.Wait();
                       },
                       item =>
                       {
                           lock (disposerLock)
                               disposed.Add(item);
                       }))
            {
                for (int i = 0; i < posted; i++)
                {
                    Assert.That(queue.Post(i), Is.True);
                }

                Assert.That(firstInvoked.Wait(TimeoutMs), Is.True, "worker did not pick up the first item");
                Assert.That(handled, Is.EqualTo(1), "only the first item should be handled before dispose");
            }

            Assert.That(disposed, Is.EqualTo(new[] { 1, 2, 3, 4 }), "remaining items must be released via the disposer");
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var queue = new SerializedCallbackQueue<int>(4, "idempotent", _ => { }, _ => { });

            queue.Dispose();
            queue.Dispose();

            Assert.Pass();
        }

        [Test]
        public void Post_AfterDispose_ReturnsFalse_AndDoesNotDisposeItem()
        {
            var handled = 0;
            var disposed = new List<int>();
            var disposerLock = new object();
            var queue = new SerializedCallbackQueue<int>(4, "after-dispose", _ => Interlocked.Increment(ref handled),
                item =>
                {
                    lock (disposerLock)
                        disposed.Add(item);
                });

            queue.Dispose();

            Assert.That(queue.Post(42), Is.False, "Post after dispose must report failure");

            Assert.That(Interlocked.CompareExchange(ref handled, 0, 0), Is.EqualTo(0));
            Assert.That(disposed, Is.Empty, "the caller keeps ownership, the item must not be disposed");
        }

        [Test]
        public void ExceptionHandler_ThrowingSubscriber_DoesNotStopQueue()
        {
            var exceptions = 0;
            var handled = 0;
            var remaining = new CountdownEvent(5);

            using (var queue = new SerializedCallbackQueue<int>(
                       10,
                       "throwing-subscriber",
                       item =>
                       {
                           Interlocked.Increment(ref handled);
                           remaining.Signal();
                           if (item == 2)
                               throw new InvalidOperationException("boom");
                       },
                       _ => { }))
            {
                queue.ExceptionHandler += _ =>
                {
                    Interlocked.Increment(ref exceptions);
                    throw new InvalidOperationException("subscriber-fail");
                };

                for (int i = 0; i < 5; i++)
                {
                    Assert.That(queue.Post(i), Is.True);
                }

                Assert.That(remaining.Wait(TimeoutMs), Is.True, "queue stopped after the ExceptionHandler subscriber threw");
            }

            Assert.That(Interlocked.CompareExchange(ref handled, 0, 0), Is.EqualTo(5));
            Assert.That(Interlocked.CompareExchange(ref exceptions, 0, 0), Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentPosts_EveryItemIsProcessedExactlyOnce()
        {
            const int count = 2000;
            const int producerCount = 4;
            var processed = new ConcurrentDictionary<int, bool>();
            var processedCount = 0;
            var producers = new List<Thread>();

            using (var queue = new SerializedCallbackQueue<int>(
                       4000,
                       "concurrent",
                       item =>
                       {
                           processed.TryAdd(item, true);
                           Interlocked.Increment(ref processedCount);
                       },
                       _ => { }))
            {
                for (int p = 0; p < producerCount; p++)
                {
                    int start = p * (count / producerCount);
                    int end = start + count / producerCount;
                    var thread = new Thread(() =>
                    {
                        for (int i = start; i < end; i++)
                        {
                            while (!queue.Post(i))
                                Thread.Yield();
                        }
                    });
                    producers.Add(thread);
                    thread.Start();
                }

                foreach (var producer in producers)
                    producer.Join();

                Assert.That(WaitFor(() => Volatile.Read(ref processedCount) == count, TimeoutMs), Is.True,
                    "not all items were processed in time");
            }

            Assert.That(processedCount, Is.EqualTo(count));
            Assert.That(processed.Keys.OrderBy(k => k).ToArray(), Is.EqualTo(Enumerable.Range(0, count).ToArray()));
        }

        private static bool WaitFor(Func<bool> condition, int timeoutMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
                Thread.Sleep(1);
            return condition();
        }
    }
}
