using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Pontifex.RR.Reliable;

namespace Pontifex.Tests.RR.RRReliableCoreHelpers;

/// <summary>
/// Fixture for the reliable server session maps. The two stress methods live in the
/// <c>Tests/Integration/</c> group because they use real parallel threads and carry the
/// integration category; the window test is deterministic.
/// </summary>
[TestFixture]
[Category("Direct.RRReliableCoreHelpers")]
public sealed class RRReliableServerSessionTests
{
    [Test]
    [Category("Direct.RRReliable.Integration")]
    public void ConcurrentMarkCompletedAndQueries_AreLockSafeAndExactlyOnce()
    {
        var session = new RRReliableServerSession();
        var errors = new ConcurrentBag<Exception>();

        Parallel.For(0, 20_000, i =>
        {
            try
            {
                ushort id = (ushort)(i % 4096);
                session.MarkCompleted(id, null);
                _ = session.IsCompleted(id);
                _ = session.IsLive(id);
                _ = session.IsFull;
                _ = session.IsClosed;
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });

        Assert.That(errors, Is.Empty);
        Assert.That(session.IsCompleted(0), Is.True);
        Assert.That(session.IsCompleted(4095), Is.True);
    }

    [Test]
    [Category("Direct.RRReliable.Integration")]
    public void ConcurrentCloseAndMarkCompleted_AreExactlyOnce()
    {
        var session = new RRReliableServerSession();
        var errors = new ConcurrentBag<Exception>();

        Parallel.Invoke(
            () =>
            {
                try
                {
                    session.Close();
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            },
            () =>
            {
                try
                {
                    for (int i = 0; i < 20_000; i++)
                    {
                        session.MarkCompleted((ushort)(i % 65536), null);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            },
            () =>
            {
                try
                {
                    for (int i = 0; i < 20_000; i++)
                    {
                        _ = session.IsCompleted((ushort)(i % 65536));
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            });

        Assert.Multiple(() =>
        {
            Assert.That(errors, Is.Empty);
            Assert.That(session.IsClosed, Is.True);
        });
    }

    [Test]
    public void CompletedWindow_IsPerSessionAndFullAfterEveryId()
    {
        var session = new RRReliableServerSession();

        for (int id = 0; id <= ushort.MaxValue; id++)
        {
            session.MarkCompleted((ushort)id, null);
        }

        Assert.That(session.IsFull, Is.True);
        Assert.That(session.IsCompleted(0), Is.True);
        Assert.That(session.IsCompleted(ushort.MaxValue), Is.True);
        Assert.That(new RRReliableServerSession().IsFull, Is.False);
    }
}
