using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Ack;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Unreliable.Ack;

[TestFixture]
public sealed class UdpRawUnreliableAckConformanceTests : RawUnreliableAckConformanceTests
{
    protected override IRawUnreliableConformanceAdapter<IRawUnreliableAckServer> CreateAdapter()
    {
        return new UdpRawUnreliableAckConformanceAdapter();
    }
}

public sealed class UdpRawUnreliableAckConformanceAdapterTests
{
    [Test]
    public void CreateFixture_CreatesUnstartedLinkedEndpoints()
    {
        using var fixture = new UdpRawUnreliableAckConformanceAdapter().CreateFixture(
            new RawUnreliableConformanceFixtureOptions { MemoryRental = MemoryRental.Shared });

        var firstClient = fixture.CreateClient();
        var secondClient = fixture.CreateClient();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsStarted, Is.False);
            Assert.That(firstClient.IsStarted, Is.False);
            Assert.That(secondClient.IsStarted, Is.False);
            Assert.That(fixture.Server.Memory, Is.SameAs(MemoryRental.Shared));
            Assert.That(firstClient.Memory, Is.SameAs(MemoryRental.Shared));
            Assert.That(secondClient.Memory, Is.SameAs(MemoryRental.Shared));
        });
    }

    [Test]
    public void CreateClient_AfterServerStartsOrStops_ReturnsUnstartedClient()
    {
        using var fixture = new UdpRawUnreliableAckConformanceAdapter().CreateFixture();

        Assert.That(fixture.Server.Init((_, _) => (IRawUnreliableHandler?)null), Is.True);
        Assert.That(fixture.Server.Start(_ => { }), Is.True);
        var runningServerClient = fixture.CreateClient();
        Assert.That(fixture.Server.Stop(), Is.True);

        var stoppedServerClient = fixture.CreateClient();

        Assert.Multiple(() =>
        {
            Assert.That(runningServerClient.IsStarted, Is.False);
            Assert.That(stoppedServerClient.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task Dispose_ResetsArmedConformanceGates()
    {
        var fixture = new UdpRawUnreliableAckConformanceAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var handler = new GateTestHandler(fixture);

        Assert.That(client.Init(handler), Is.True);
        Assert.That(fixture.Server.Init((_, _) => (IRawUnreliableHandler?)null), Is.True);
        Assert.That(fixture.Server.Start(_ => { }), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);

        var endpoint = await WaitForEndpointAsync(handler);
        var serverControl = GetControl<IRawUnreliableTransportConformanceControl>(fixture.Server);
        var clientControl = GetControl<IRawUnreliableTransportConformanceControl>(client);
        var endpointControl = GetEndpointControl<IRawUnreliableEndpointConformanceControl>(endpoint);

        _ = serverControl.BeforeStopStateTransitionGate.Arm();
        _ = serverControl.BeforeStoppedCallbackGate.Arm();
        _ = serverControl.BeforeHandlerFactoryGate.Arm();
        _ = serverControl.BeforeHandlerStartedGate.Arm();
        _ = clientControl.BeforeStopStateTransitionGate.Arm();
        _ = clientControl.BeforeStoppedCallbackGate.Arm();
        _ = clientControl.BeforeHandlerStartedGate.Arm();
        _ = endpointControl.BeforeEndpointStopStateTransitionGate.Arm();
        _ = endpointControl.BeforeHandlerStoppedGate.Arm();
        _ = endpointControl.BeforeSendCommitGate.Arm();
        _ = endpointControl.AfterSendCommitGate.Arm();
        _ = endpointControl.AfterReceivedGate.Arm();

        fixture.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(serverControl.BeforeStopStateTransitionGate.IsArmed, Is.False);
            Assert.That(serverControl.BeforeStoppedCallbackGate.IsArmed, Is.False);
            Assert.That(serverControl.BeforeHandlerFactoryGate.IsArmed, Is.False);
            Assert.That(serverControl.BeforeHandlerStartedGate.IsArmed, Is.False);
            Assert.That(clientControl.BeforeStopStateTransitionGate.IsArmed, Is.False);
            Assert.That(clientControl.BeforeStoppedCallbackGate.IsArmed, Is.False);
            Assert.That(clientControl.BeforeHandlerStartedGate.IsArmed, Is.False);
            Assert.That(endpointControl.BeforeEndpointStopStateTransitionGate.IsArmed, Is.False);
            Assert.That(endpointControl.BeforeHandlerStoppedGate.IsArmed, Is.False);
            Assert.That(endpointControl.BeforeSendCommitGate.IsArmed, Is.False);
            Assert.That(endpointControl.AfterSendCommitGate.IsArmed, Is.False);
            Assert.That(endpointControl.AfterReceivedGate.IsArmed, Is.False);
        });
    }

    private static async Task<IRawUnreliableEndpoint> WaitForEndpointAsync(RawUnreliableTestHandler handler)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (handler.Endpoint == null)
        {
            if (DateTime.UtcNow >= deadline)
                Assert.Fail("OnStarted was not invoked within the delivery timeout.");
            await Task.Delay(10);
        }
        return handler.Endpoint;
    }

    private static TControl GetControl<TControl>(ITransport transport)
        where TControl : class, IControl
    {
        var controls = new List<IControl>();
        transport.GetControls(controls, control => control is TControl);
        return controls.OfType<TControl>().Single();
    }

    private static TControl GetEndpointControl<TControl>(IRawUnreliableEndpoint endpoint)
        where TControl : class, IControl
    {
        var controls = new List<IControl>();
        endpoint.GetControls(controls, control => control is TControl);
        return controls.OfType<TControl>().Single();
    }

    private sealed class GateTestHandler : RawUnreliableTestHandler
    {
        public GateTestHandler(IRawUnreliableConformanceFixture<IRawUnreliableAckServer> fixture)
            : base(fixture)
        {
        }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }
    }
}
