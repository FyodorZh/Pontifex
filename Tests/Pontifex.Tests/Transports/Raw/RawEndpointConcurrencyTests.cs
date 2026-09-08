using System.Collections.Concurrent;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Pontifex.Tests.Transports.Raw.Support;

namespace Pontifex.Tests.Transports.Raw;

/// <summary>
/// Multi-threaded property suite for RawEndpoint. These tests assert only
/// interleaving-independent invariants:
/// - every Ok-admitted message is delivered exactly once and released exactly once
///   (no leak, no duplicate) unless it was dropped by a racing stop, in which case it
///   is still released exactly once;
/// - per-producer send order is preserved in the delivery order;
/// - handler OnStopped is delivered exactly once and no OnReceived begins after it;
/// - Send racing a stop returns only Ok or NotConnected.
/// Exact Ok/NotConnected splits and exact byte-counter net-zero are NOT asserted here
/// (they are non-deterministic / not observable without a production seam).
/// </summary>
public sealed class RawEndpointConcurrencyTests
{
    private const int ProducerCount = 4;
    private const int SendsPerProducer = 150;
    private const int MarkerStride = 10000;

    private static void DrainUntilQuiet(FakeNonPeriodicDriver driver)
    {
        for (int i = 0; i < 200_000 && driver.IsRunning; i++)
        {
            if (!driver.HasPendingInvocation)
            {
                break;
            }

            driver.DrainPending();
            Thread.Yield();
        }
    }

    private static (int Producer, int Seq) MarkerParts(int marker)
    {
        return (marker / MarkerStride, marker % MarkerStride);
    }

    [Test]
    public void ConcurrentSends_NoStop_PerProducerOrderPreserved_NoLossNoDuplicates()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        var buffers = new UnionDataList[ProducerCount, SendsPerProducer];
        for (int p = 0; p < ProducerCount; p++)
        {
            for (int s = 0; s < SendsPerProducer; s++)
            {
                buffers[p, s] = RawTestBuffers.Create(c.Memory, marker: p * MarkerStride + s);
            }
        }

        var errors = new ConcurrentQueue<Exception>();
        var okCount = 0;
        var started = new ManualResetEventSlim(false);

        var producers = Enumerable.Range(0, ProducerCount).Select(p => Task.Run(() =>
        {
            started.Wait();
            int ok = 0;
            for (int s = 0; s < SendsPerProducer; s++)
            {
                try
                {
                    var res = c.Endpoint.Send(buffers[p, s]);
                    if (res == SendResult.Ok)
                    {
                        ok++;
                    }
                    else
                    {
                        errors.Enqueue(new InvalidOperationException($"producer {p} send {s} -> {res}"));
                    }
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }

            Interlocked.Add(ref okCount, ok);
        })).ToArray();

        started.Set();
        Task.WaitAll(producers);

        DrainUntilQuiet(c.Driver);
        // A tiny settle: invocation may have been requested between the drain exit check
        // and the producer join completing.
        DrainUntilQuiet(c.Driver);

        Assert.That(errors, Is.Empty);
        Assert.That(okCount, Is.EqualTo(ProducerCount * SendsPerProducer));

        var delivered = c.Endpoint.DeliveredMarkers;
        Assert.Multiple(() =>
        {
            Assert.That(delivered, Has.Count.EqualTo(ProducerCount * SendsPerProducer));
            Assert.That(delivered, Is.Unique);
        });

        // Each producer's own markers must appear in increasing order.
        for (int p = 0; p < ProducerCount; p++)
        {
            var seqs = delivered.Where(m => MarkerParts(m).Producer == p).Select(m => MarkerParts(m).Seq).ToArray();
            Assert.That(seqs, Is.Ordered.Ascending);
            Assert.That(seqs, Is.EquivalentTo(Enumerable.Range(0, SendsPerProducer)));
        }

        // Every accepted buffer was delivered and therefore released.
        for (int p = 0; p < ProducerCount; p++)
        {
            for (int s = 0; s < SendsPerProducer; s++)
            {
                Assert.That(buffers[p, s].IsAlive, Is.False);
            }
        }
    }

    [Test]
    public void ConcurrentSends_StopNowRace_OnlyOkOrNotConnected_NoLeak_NoDuplicates()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        var buffers = new UnionDataList[ProducerCount, SendsPerProducer];
        for (int p = 0; p < ProducerCount; p++)
        {
            for (int s = 0; s < SendsPerProducer; s++)
            {
                buffers[p, s] = RawTestBuffers.Create(c.Memory, marker: p * MarkerStride + s);
            }
        }

        var okMarkers = new ConcurrentDictionary<int, bool>();
        var errors = new ConcurrentQueue<Exception>();
        var started = new ManualResetEventSlim(false);

        var producers = Enumerable.Range(0, ProducerCount).Select(p => Task.Run(() =>
        {
            started.Wait();
            for (int s = 0; s < SendsPerProducer; s++)
            {
                int marker = p * MarkerStride + s;
                try
                {
                    var res = c.Endpoint.Send(buffers[p, s]);
                    switch (res)
                    {
                        case SendResult.Ok:
                            okMarkers.TryAdd(marker, true);
                            break;
                        case SendResult.NotConnected:
                            break;
                        default:
                            errors.Enqueue(new InvalidOperationException($"producer {p} send {s} -> unexpected {res}"));
                            break;
                    }
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }
        })).ToArray();

        started.Set();
        Thread.Sleep(1);
        c.Endpoint.StopNow(new TextFail("unit", "abrupt"));
        c.Driver.SimulateLogicStopped();

        Task.WaitAll(producers);
        DrainUntilQuiet(c.Driver);

        Assert.That(errors, Is.Empty);
        Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));

        var delivered = c.Endpoint.DeliveredMarkers;
        Assert.Multiple(() =>
        {
            Assert.That(delivered, Is.Unique);
            // Only Ok-admitted messages may be delivered.
            Assert.That(delivered.All(m => okMarkers.ContainsKey(m)), Is.True);
        });

        // Every created buffer must have been released exactly once by some path:
        // delivered (harness/DoSendRaw), dropped by the valve drain, or rejected inline.
        for (int p = 0; p < ProducerCount; p++)
        {
            for (int s = 0; s < SendsPerProducer; s++)
            {
                Assert.That(buffers[p, s].IsAlive, Is.False);
            }
        }
    }

    [Test]
    public void ConcurrentSends_StopGracefullyRace_OnlyOkOrNotConnected_NoLeak_NoDuplicates()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        var buffers = new UnionDataList[ProducerCount, SendsPerProducer];
        for (int p = 0; p < ProducerCount; p++)
        {
            for (int s = 0; s < SendsPerProducer; s++)
            {
                buffers[p, s] = RawTestBuffers.Create(c.Memory, marker: p * MarkerStride + s);
            }
        }

        var okMarkers = new ConcurrentDictionary<int, bool>();
        var errors = new ConcurrentQueue<Exception>();
        var started = new ManualResetEventSlim(false);

        var producers = Enumerable.Range(0, ProducerCount).Select(p => Task.Run(() =>
        {
            started.Wait();
            for (int s = 0; s < SendsPerProducer; s++)
            {
                int marker = p * MarkerStride + s;
                try
                {
                    var res = c.Endpoint.Send(buffers[p, s]);
                    switch (res)
                    {
                        case SendResult.Ok:
                            okMarkers.TryAdd(marker, true);
                            break;
                        case SendResult.NotConnected:
                            break;
                        default:
                            errors.Enqueue(new InvalidOperationException($"producer {p} send {s} -> unexpected {res}"));
                            break;
                    }
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }
        })).ToArray();

        started.Set();
        Thread.Sleep(1);
        Assert.That(c.Endpoint.StopGracefully(new TextFail("unit", "graceful")), Is.True);

        Task.WaitAll(producers);
        DrainUntilQuiet(c.Driver);
        // Guarantee the graceful Stop marker is consumed even if no invocation was pending.
        if (c.Driver.IsRunning)
        {
            c.Driver.TickOnce();
            DrainUntilQuiet(c.Driver);
        }

        Assert.That(errors, Is.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));
            Assert.That(c.Driver.StopRequestCount, Is.EqualTo(1));
        });

        var delivered = c.Endpoint.DeliveredMarkers;
        Assert.Multiple(() =>
        {
            Assert.That(delivered, Is.Unique);
            Assert.That(delivered.All(m => okMarkers.ContainsKey(m)), Is.True);
        });

        for (int p = 0; p < ProducerCount; p++)
        {
            for (int s = 0; s < SendsPerProducer; s++)
            {
                Assert.That(buffers[p, s].IsAlive, Is.False);
            }
        }
    }

    [Test]
    public void InboundVsStop_NoOnReceivedAfterOnStopped()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        const int inboundCount = 400;
        var messages = new UnionDataList[inboundCount];
        for (int i = 0; i < inboundCount; i++)
        {
            messages[i] = RawTestBuffers.Create(c.Memory, marker: i);
        }

        var started = new ManualResetEventSlim(false);
        var errors = new ConcurrentQueue<Exception>();
        var deliverers = Enumerable.Range(0, 4).Select(t => Task.Run(() =>
        {
            started.Wait();
            for (int i = t; i < inboundCount; i += 4)
            {
                try
                {
                    c.Endpoint.DeliverInbound(messages[i]);
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }
        })).ToArray();

        started.Set();
        Thread.Sleep(1);
        c.Endpoint.StopNow(new TextFail("unit", "stop"));
        c.Driver.SimulateLogicStopped();

        Task.WaitAll(deliverers);

        Assert.That(errors, Is.Empty);

        var events = c.Handler.Events;
        int firstStoppedIndex = -1;
        for (int i = 0; i < events.Count; i++)
        {
            if (!events[i].IsReceived)
            {
                firstStoppedIndex = i;
                break;
            }
        }

        Assert.That(firstStoppedIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));
        for (int i = firstStoppedIndex + 1; i < events.Count; i++)
        {
            Assert.That(events[i].IsReceived, Is.False,
                $"received event #{events[i].Seq} began after the stopped event #{events[firstStoppedIndex].Seq}");
        }

        // Every inbound buffer was consumed by either the handler (delivered, released)
        // or the base drop path (released): no leaks.
        for (int i = 0; i < inboundCount; i++)
        {
            Assert.That(messages[i].IsAlive, Is.False);
        }
    }
}
