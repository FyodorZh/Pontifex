using System;
using Pontifex.Tests.RR.TestSupport;

namespace Pontifex.Tests.RR.RRReliableCoreHelpers;

[TestFixture]
[Category("Direct.RRReliableCoreHelpers")]
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
