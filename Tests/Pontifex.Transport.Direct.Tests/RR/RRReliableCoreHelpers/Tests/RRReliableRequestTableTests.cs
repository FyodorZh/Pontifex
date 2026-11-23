using System.Collections.Generic;
using System.Threading.Tasks;
using Pontifex.RR.Reliable;
using Pontifex.Tests.RR.RRReliableCoreHelpers.Harness;
using Pontifex.Utils;

namespace Pontifex.Tests.RR.RRReliableCoreHelpers;

[TestFixture]
[Category("Direct.RRReliableCoreHelpers")]
public sealed class RRReliableRequestTableTests
{
    [Test]
    public void AddAndGet_ReturnsTheEntryWithoutRemovingIt()
    {
        var table = new RRReliableRequestTable();
        var handler = CreateHandler(out RRReliableRequestState state);

        Assert.That(table.TryAdd(10, handler, state), Is.True);
        Assert.That(table.TryGet(10, out RRReliableRequestEntry first), Is.True);
        Assert.That(table.TryGet(10, out RRReliableRequestEntry second), Is.True);
        Assert.That(first.State, Is.SameAs(state));
        Assert.That(second.Handler, Is.SameAs(handler));
        Assert.That(table.Count, Is.EqualTo(1));
    }

    [Test]
    public void AddDuplicate_ReturnsFalseAndKeepsTheFirstEntry()
    {
        var table = new RRReliableRequestTable();
        var first = CreateHandler(out RRReliableRequestState firstState);
        var second = CreateHandler(out RRReliableRequestState secondState);

        Assert.That(table.TryAdd(1, first, firstState), Is.True);
        Assert.That(table.TryAdd(1, second, secondState), Is.False);
        Assert.That(table.TryGet(1, out RRReliableRequestEntry entry), Is.True);
        Assert.That(entry.State, Is.SameAs(firstState));
        Assert.That(table.Count, Is.EqualTo(1));
    }

    [Test]
    public void TakeExact_RemovesOnlyTheMatchingState()
    {
        var table = new RRReliableRequestTable();
        var handler = CreateHandler(out RRReliableRequestState state);
        var other = CreateHandler(out RRReliableRequestState otherState);

        Assert.That(table.TryAdd(5, handler, state), Is.True);
        Assert.That(table.TryTakeExact(5, otherState, out _), Is.False);
        Assert.That(table.Count, Is.EqualTo(1));
        Assert.That(table.TryTakeExact(5, state, out RRReliableRequestEntry entry), Is.True);
        Assert.That(entry.Handler, Is.SameAs(handler));
        Assert.That(table.Count, Is.Zero);
        Assert.That(table.TryGet(5, out _), Is.False);
    }

    [Test]
    public void Remove_RemovesTheEntryAndReportsWhetherItExisted()
    {
        var table = new RRReliableRequestTable();
        var handler = CreateHandler(out RRReliableRequestState state);

        Assert.That(table.TryAdd(2, handler, state), Is.True);
        Assert.That(table.TryRemove(2, out RRReliableRequestEntry entry), Is.True);
        Assert.That(entry.State, Is.SameAs(state));
        Assert.That(table.TryRemove(2, out _), Is.False);
    }

    [Test]
    public void Clear_InvokesEveryEntryAndEmptiesTheTable()
    {
        var table = new RRReliableRequestTable();
        var ids = new List<ushort>();

        for (ushort id = 1; id <= 4; id++)
        {
            Assert.That(table.TryAdd(id, CreateHandler(id, out RRReliableRequestState state), state),
                Is.True);
            ids.Add(id);
        }

        var removed = new List<ushort>();
        table.Clear(entry => removed.Add(entry.State.ClientLocalId));

        Assert.That(removed, Is.EquivalentTo(ids));
        Assert.That(table.Count, Is.Zero);
    }

    [Test]
    public void ConcurrentAccess_AddsEveryEntryAndReadsThemSafely()
    {
        var table = new RRReliableRequestTable();

        Parallel.For(0, 512, id =>
        {
            ushort requestId = (ushort)id;
            Assert.That(table.TryAdd(requestId,
                CreateHandler(requestId, out RRReliableRequestState state), state), Is.True);
        });

        Assert.That(table.Count, Is.EqualTo(512));

        Parallel.For(0, 512, id =>
        {
            Assert.That(table.TryGet((ushort)id, out RRReliableRequestEntry entry), Is.True);
            Assert.That(entry.State.ClientLocalId, Is.EqualTo((ushort)id));
        });
    }

    private static RecordingReliableRequestHandler CreateHandler(out RRReliableRequestState state)
    {
        return CreateHandler(0, out state);
    }

    private static RecordingReliableRequestHandler CreateHandler(ushort requestId,
        out RRReliableRequestState state)
    {
        var handler = new RecordingReliableRequestHandler();
        state = new RRReliableRequestState(requestId, handler);
        return handler;
    }
}
