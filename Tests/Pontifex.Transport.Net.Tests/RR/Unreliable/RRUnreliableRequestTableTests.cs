using System;
using System.Collections.Generic;
using NUnit.Framework;
using Operarius;
using Pontifex.RR.Unreliable;

namespace Pontifex.Tests.RR.Unreliable;

/// <summary>
/// Deterministic checks of the client pending-request table: expiry reporting, re-arming, identity,
/// and pruning of expired entries.
/// </summary>
[TestFixture]
public sealed class RRUnreliableRequestTableTests
{
    private static readonly DateTime Start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void TryTake_LiveEntry_ReportsNotExpired()
    {
        var clock = new ManualClock { Now = Start };
        var table = new RRUnreliableRequestTable<Box>(clock, TimeSpan.FromSeconds(10));
        var value = new Box(42);
        table.TryAdd(1, value);
        table.ReArm(1);

        clock.Now = Start + TimeSpan.FromSeconds(9);

        Assert.That(table.TryTake(1, out Box taken, out bool expired), Is.True);
        Assert.That(taken, Is.SameAs(value));
        Assert.That(expired, Is.False);
    }

    [Test]
    public void TryTake_ExpiredEntry_ReportsExpired()
    {
        var clock = new ManualClock { Now = Start };
        var table = new RRUnreliableRequestTable<Box>(clock, TimeSpan.FromSeconds(10));
        var value = new Box(42);
        table.TryAdd(1, value);
        table.ReArm(1);

        clock.Now = Start + TimeSpan.FromSeconds(11);

        Assert.That(table.TryTake(1, out Box taken, out bool expired), Is.True);
        Assert.That(taken, Is.SameAs(value));
        Assert.That(expired, Is.True);
    }

    [Test]
    public void Add_PrunesExpiredEntriesAndNotifies()
    {
        var clock = new ManualClock { Now = Start };
        var table = new RRUnreliableRequestTable<Box>(clock, TimeSpan.FromSeconds(10));
        var pruned = new List<Box>();
        var expired = new Box(42);
        table.TryAdd(1, expired);
        table.ReArm(1);

        clock.Now = Start + TimeSpan.FromSeconds(11);
        table.TryAdd(2, new Box(7), pruned.Add);

        Assert.That(pruned, Has.Count.EqualTo(1));
        Assert.That(pruned[0], Is.SameAs(expired));
        Assert.That(table.TryTake(1, out _, out _), Is.False);
    }

    [Test]
    public void ReArm_ExtendsDeadline()
    {
        var clock = new ManualClock { Now = Start };
        var table = new RRUnreliableRequestTable<Box>(clock, TimeSpan.FromSeconds(10));
        var value = new Box(42);
        table.TryAdd(1, value);
        table.ReArm(1);

        clock.Now = Start + TimeSpan.FromSeconds(9);
        table.ReArm(1);

        clock.Now = Start + TimeSpan.FromSeconds(18);

        Assert.That(table.TryTake(1, out Box taken, out bool expired), Is.True);
        Assert.That(taken, Is.SameAs(value));
        Assert.That(expired, Is.False);
    }

    [Test]
    public void TryTake_ProvisionalEntry_StaysLiveUntilReArmed()
    {
        var clock = new ManualClock { Now = Start };
        var table = new RRUnreliableRequestTable<Box>(clock, TimeSpan.FromSeconds(10));
        var value = new Box(42);
        table.TryAdd(1, value);

        clock.Now = Start + TimeSpan.FromSeconds(11);

        Assert.That(table.TryTake(1, out Box taken, out bool expired), Is.True);
        Assert.That(taken, Is.SameAs(value));
        Assert.That(expired, Is.False);
    }

    [Test]
    public void TryReapExpired_RemovesOnlyAnExpiredMatchingEntry()
    {
        var clock = new ManualClock { Now = Start };
        var table = new RRUnreliableRequestTable<Box>(clock, TimeSpan.FromSeconds(10));
        var value = new Box(42);
        table.TryAdd(1, value);
        table.ReArm(1);

        clock.Now = Start + TimeSpan.FromSeconds(5);
        Assert.That(table.TryReapExpired(1, value), Is.False);

        clock.Now = Start + TimeSpan.FromSeconds(11);
        Assert.That(table.TryReapExpired(1, new Box(42)), Is.False);
        Assert.That(table.TryReapExpired(1, value), Is.True);
        Assert.That(table.TryTake(1, out _, out _), Is.False);
    }

    private sealed class Box
    {
        public Box(int number)
        {
            Number = number;
        }

        public int Number { get; }
    }

    private sealed class ManualClock : IDateTimeProvider
    {
        public DateTime Now { get; set; }
    }
}
