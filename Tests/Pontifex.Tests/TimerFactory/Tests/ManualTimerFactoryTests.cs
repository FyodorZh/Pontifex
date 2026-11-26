using System;
using System.Threading;
using Pontifex.Utils;

namespace Pontifex.Tests.TimerFactory;

[TestFixture]
[Category("TimerFactory")]
public sealed class ManualTimerFactoryTests
{
    [Test]
    public void Schedule_RecordsTheDelayAndCallback()
    {
        var factory = new ManualTimerFactory();

        IDisposable handle = factory.Schedule(TimeSpan.FromSeconds(3), () => { });

        Assert.That(factory.ScheduledCount, Is.EqualTo(1));
        Assert.That(factory.PendingCount, Is.EqualTo(1));
        Assert.That(((ManualTimerHandle)handle).Delay, Is.EqualTo(TimeSpan.FromSeconds(3)));
    }

    [Test]
    public void Schedule_WithInfiniteDelay_RecordsAPendingHandleThatNeverFiresOnItsOwn()
    {
        var factory = new ManualTimerFactory();
        int fired = 0;
        factory.Schedule(Timeout.InfiniteTimeSpan, () => fired++);

        Assert.Multiple(() =>
        {
            Assert.That(factory.ScheduledCount, Is.EqualTo(1));
            Assert.That(factory.PendingCount, Is.EqualTo(1));
            Assert.That(factory.Handles[0].Delay, Is.EqualTo(Timeout.InfiniteTimeSpan));
            Assert.That(fired, Is.Zero);
        });
    }

    [Test]
    public void Fire_RunsTheCallbackOnlyOnce()
    {
        var factory = new ManualTimerFactory();
        int fired = 0;
        factory.Schedule(TimeSpan.FromSeconds(1), () => fired++);

        Assert.That(factory.Fire(0), Is.True);
        Assert.That(fired, Is.EqualTo(1));
        Assert.That(factory.Fire(0), Is.False);
        Assert.That(fired, Is.EqualTo(1));
        Assert.That(factory.PendingCount, Is.Zero);
        Assert.That(factory.Handles[0].IsFired, Is.True);
    }

    [Test]
    public void Suppress_ClosesTheHandleWithoutRunningTheCallback()
    {
        var factory = new ManualTimerFactory();
        int fired = 0;
        factory.Schedule(TimeSpan.FromSeconds(1), () => fired++);

        factory.Handles[0].Suppress();

        Assert.That(factory.PendingCount, Is.Zero);
        Assert.That(factory.Fire(0), Is.False);
        Assert.That(fired, Is.Zero);
        Assert.That(factory.Handles[0].IsClosed, Is.True);
    }

    [Test]
    public void Dispose_ClosesTheHandleWithoutRunningTheCallback()
    {
        var factory = new ManualTimerFactory();
        int fired = 0;
        IDisposable handle = factory.Schedule(TimeSpan.FromSeconds(1), () => fired++);

        handle.Dispose();
        handle.Dispose();

        Assert.That(factory.PendingCount, Is.Zero);
        Assert.That(factory.Fire(0), Is.False);
        Assert.That(fired, Is.Zero);
    }

    [Test]
    public void FireAll_RunsEveryPendingCallback()
    {
        var factory = new ManualTimerFactory();
        int fired = 0;
        factory.Schedule(TimeSpan.FromSeconds(1), () => fired++);
        factory.Schedule(TimeSpan.FromSeconds(2), () => fired++);

        Assert.That(factory.FireAll(), Is.EqualTo(2));
        Assert.That(fired, Is.EqualTo(2));
        Assert.That(factory.PendingCount, Is.Zero);
    }

    [Test]
    public void Clear_SuppressesPendingHandlesAndResetsTheCounts()
    {
        var factory = new ManualTimerFactory();
        int fired = 0;
        factory.Schedule(TimeSpan.FromSeconds(1), () => fired++);
        factory.Schedule(TimeSpan.FromSeconds(2), () => fired++);

        factory.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(factory.ScheduledCount, Is.Zero);
            Assert.That(factory.PendingCount, Is.Zero);
            Assert.That(factory.Handles, Is.Empty);
            Assert.That(factory.FireAll(), Is.Zero);
            Assert.That(fired, Is.Zero);
        });
    }

    [Test]
    public void Clear_WhenNothingIsScheduled_IsSafe()
    {
        var factory = new ManualTimerFactory();

        factory.Clear();

        Assert.That(factory.ScheduledCount, Is.Zero);
    }

    [Test]
    public void Advance_FiresDueCallbacksInDueOrder()
    {
        var factory = new ManualTimerFactory();
        var order = new List<int>();
        factory.Schedule(TimeSpan.FromSeconds(2), () => order.Add(2));
        factory.Schedule(TimeSpan.FromSeconds(1), () => order.Add(1));

        factory.Advance(TimeSpan.FromSeconds(3));

        Assert.Multiple(() =>
        {
            Assert.That(order, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(factory.PendingCount, Is.Zero);
        });
    }

    [Test]
    public void Advance_DoesNotFireAFutureCallback()
    {
        var factory = new ManualTimerFactory();
        int fired = 0;
        factory.Schedule(TimeSpan.FromSeconds(10), () => fired++);

        factory.Advance(TimeSpan.FromSeconds(9));
        Assert.That(fired, Is.Zero);
        Assert.That(factory.PendingCount, Is.EqualTo(1));

        factory.Advance(TimeSpan.FromSeconds(1));
        Assert.That(fired, Is.EqualTo(1));
        Assert.That(factory.PendingCount, Is.Zero);
    }

    [Test]
    public void Advance_FiresACallbackScheduledWithZeroDelayDuringTheAdvance()
    {
        var factory = new ManualTimerFactory();
        int secondFired = 0;
        factory.Schedule(TimeSpan.FromSeconds(1), () => factory.Schedule(TimeSpan.Zero, () => secondFired++));

        factory.Advance(TimeSpan.FromSeconds(1));

        Assert.That(secondFired, Is.EqualTo(1));
        Assert.That(factory.PendingCount, Is.Zero);
    }

    [Test]
    public void Advance_WithInfiniteDelay_NeverFiresTheHandle()
    {
        var factory = new ManualTimerFactory();
        int fired = 0;
        factory.Schedule(Timeout.InfiniteTimeSpan, () => fired++);

        factory.Advance(TimeSpan.FromDays(1));

        Assert.Multiple(() =>
        {
            Assert.That(fired, Is.Zero);
            Assert.That(factory.PendingCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Advance_MovesNowByTheRequestedAmount()
    {
        var factory = new ManualTimerFactory();
        DateTime start = factory.Now;

        factory.Advance(TimeSpan.FromSeconds(5));

        Assert.That(factory.Now, Is.EqualTo(start + TimeSpan.FromSeconds(5)));
    }

    [Test]
    public void Advance_WithNegativeBy_Throws()
    {
        var factory = new ManualTimerFactory();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => factory.Advance(TimeSpan.FromSeconds(-1)));
    }

    [Test]
    public void PendingDelays_IsOrderedByDueTime()
    {
        var factory = new ManualTimerFactory();
        factory.Schedule(TimeSpan.FromSeconds(5), () => { });
        factory.Schedule(TimeSpan.FromSeconds(1), () => { });

        Assert.That(factory.PendingDelays,
            Is.EqualTo(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5) }));
    }

    [Test]
    public void Clear_DoesNotResetNow()
    {
        var factory = new ManualTimerFactory();
        factory.Advance(TimeSpan.FromSeconds(5));
        DateTime now = factory.Now;

        factory.Clear();

        Assert.That(factory.Now, Is.EqualTo(now));
    }

    [Test]
    public void Schedule_WithNullCallback_Throws()
    {
        var factory = new ManualTimerFactory();

        Assert.Throws<ArgumentNullException>(
            () => factory.Schedule(TimeSpan.FromSeconds(1), null!));
    }

    [Test]
    public void Schedule_WithNegativeDelay_Throws()
    {
        var factory = new ManualTimerFactory();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => factory.Schedule(TimeSpan.FromSeconds(-1), () => { }));
    }
}
