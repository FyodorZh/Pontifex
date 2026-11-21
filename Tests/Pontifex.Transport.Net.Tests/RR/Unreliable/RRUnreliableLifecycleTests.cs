using System;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex.RR.Unreliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.RR.Unreliable;

/// <summary>
/// Deterministic checks of the RR unreliable start and stop rules: a failed start must not deliver
/// <c>onStopped</c> and must clean up the carrier.
/// </summary>
[TestFixture]
public sealed class RRUnreliableLifecycleTests
{
    [Test]
    public void FailedCarrierStart_DoesNotInvokeOnStopped()
    {
        var server = new ThrowingServerTransport();
        var handler = new AcceptingHandler();

        Assert.That(server.Init(handler), Is.True);

        bool stopped = false;
        Assert.That(server.Start(_ => stopped = true), Is.False);

        Assert.That(stopped, Is.False);
        Assert.That(server.IsValid, Is.False);
        Assert.That(server.CarrierStopped, Is.True);
    }

    private sealed class ThrowingServerTransport : RRUnreliableServerTransport
    {
        public ThrowingServerTransport()
            : base("test-rr-throwing", TimeSpan.FromSeconds(10), new Logger([]), MemoryRental.Shared)
        {
        }

        public override int MessageMaxByteSize => 100;

        public bool CarrierStopped { get; private set; }

        protected override bool StartCarrier()
        {
            throw new InvalidOperationException("start failed");
        }

        protected override void StopCarrier(StopReason reason)
        {
            CarrierStopped = true;
        }

        protected override SendResult SendFrame(IEndPoint destination, UnionDataList frame)
        {
            frame.Release();
            return SendResult.Error;
        }
    }

    private sealed class AcceptingHandler : IRRUnreliableServerHandler
    {
        public bool OnRequested(IEndPoint client, UnionDataList request, IRRUnreliableResponse response)
        {
            request.Release();
            return true;
        }
    }
}
