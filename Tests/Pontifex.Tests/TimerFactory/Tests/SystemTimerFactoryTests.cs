using System;
using System.Threading;
using Pontifex.Utils;

namespace Pontifex.Tests.TimerFactory;

[TestFixture]
[Category("TimerFactory")]
public sealed class SystemTimerFactoryTests
{
    [Test]
    public void Schedule_WithNullCallback_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => SystemTimerFactory.Instance.Schedule(TimeSpan.FromSeconds(1), null!));
    }

    [Test]
    public void Schedule_WithNegativeDelay_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SystemTimerFactory.Instance.Schedule(TimeSpan.FromSeconds(-1), () => { }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SystemTimerFactory.Instance.Schedule(TimeSpan.FromMilliseconds(-2), () => { }));
        });
    }

    [Test]
    public void Schedule_WithInfiniteDelay_ReturnsAHandleThatNeverFires()
    {
        int fired = 0;
        IDisposable handle = SystemTimerFactory.Instance.Schedule(
            Timeout.InfiniteTimeSpan, () => Interlocked.Increment(ref fired));

        handle.Dispose();
        handle.Dispose();

        Assert.That(Volatile.Read(ref fired), Is.Zero);
    }

    [Test]
    public void Schedule_WithALongDelay_DoesNotFireSynchronouslyAndDisposeCancelsIt()
    {
        int fired = 0;
        IDisposable handle = SystemTimerFactory.Instance.Schedule(
            TimeSpan.FromHours(1), () => Interlocked.Increment(ref fired));

        Assert.That(Volatile.Read(ref fired), Is.Zero);

        handle.Dispose();

        Assert.That(Volatile.Read(ref fired), Is.Zero);
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        IDisposable handle = SystemTimerFactory.Instance.Schedule(
            TimeSpan.FromHours(1), () => { });

        handle.Dispose();
        handle.Dispose();
    }

    [Test]
    [Category("TimerFactory.Integration")]
    public void Schedule_WithAShortDelay_FiresTheCallbackOnce()
    {
        using var fired = new ManualResetEventSlim(false);
        int count = 0;
        IDisposable handle = SystemTimerFactory.Instance.Schedule(
            TimeSpan.FromMilliseconds(20), () =>
            {
                Interlocked.Increment(ref count);
                fired.Set();
            });

        try
        {
            Assert.That(fired.Wait(TimeSpan.FromSeconds(5)), Is.True,
                "The timer did not fire in time.");
            Assert.That(Volatile.Read(ref count), Is.EqualTo(1));
        }
        finally
        {
            handle.Dispose();
        }
    }

    [Test]
    [Category("TimerFactory.Integration")]
    public void Schedule_WhenTheCallbackThrows_KeepsOtherTimersAlive()
    {
        using var threw = new ManualResetEventSlim(false);
        using var survived = new ManualResetEventSlim(false);
        IDisposable throwing = SystemTimerFactory.Instance.Schedule(
            TimeSpan.FromMilliseconds(20), () =>
            {
                threw.Set();
                throw new InvalidOperationException("Expected callback failure.");
            });
        IDisposable following = SystemTimerFactory.Instance.Schedule(
            TimeSpan.FromMilliseconds(60), () => survived.Set());

        try
        {
            Assert.That(threw.Wait(TimeSpan.FromSeconds(5)), Is.True,
                "The throwing callback did not run in time.");
            Assert.That(survived.Wait(TimeSpan.FromSeconds(5)), Is.True,
                "A throwing callback escaped the factory or stopped other timers.");
        }
        finally
        {
            throwing.Dispose();
            following.Dispose();
        }
    }
}
