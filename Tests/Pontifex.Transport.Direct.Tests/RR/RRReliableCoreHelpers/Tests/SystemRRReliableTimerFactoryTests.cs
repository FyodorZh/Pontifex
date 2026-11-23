using System;
using System.Threading;
using Pontifex.RR.Reliable;

namespace Pontifex.Tests.RR.RRReliableCoreHelpers;

[TestFixture]
[Category("Direct.RRReliableCoreHelpers")]
public sealed class SystemRRReliableTimerFactoryTests
{
    [Test]
    public void Schedule_WithNullCallback_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => SystemRRReliableTimerFactory.Instance.Schedule(TimeSpan.FromSeconds(1), null!));
    }

    [Test]
    public void Schedule_WithNegativeDelay_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SystemRRReliableTimerFactory.Instance.Schedule(TimeSpan.FromSeconds(-1), () => { }));
    }

    [Test]
    public void Schedule_WithALongDelay_DoesNotFireSynchronouslyAndDisposeCancelsIt()
    {
        int fired = 0;
        IDisposable handle = SystemRRReliableTimerFactory.Instance.Schedule(
            TimeSpan.FromHours(1), () => Interlocked.Increment(ref fired));

        Assert.That(Volatile.Read(ref fired), Is.Zero);

        handle.Dispose();

        Assert.That(Volatile.Read(ref fired), Is.Zero);
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        IDisposable handle = SystemRRReliableTimerFactory.Instance.Schedule(
            TimeSpan.FromHours(1), () => { });

        handle.Dispose();
        handle.Dispose();
    }
}
