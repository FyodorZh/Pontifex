using Pontifex.StopReasons;
using Pontifex.Utils;
using Pontifex.Tests.Transports.Raw.Support;

namespace Pontifex.Tests.Transports.Raw;

/// <summary>
/// Deterministic single-threaded behavioural suite for RawEndpoint. The driver is
/// ticked explicitly (test thread == driver thread), so delivery order, counts and
/// callback sequences are fully deterministic.
/// </summary>
public sealed class RawEndpointTests
{
    private static void DrainPending(FakeNonPeriodicDriver driver)
    {
        while (driver.HasPendingInvocation && driver.IsRunning)
        {
            driver.DrainPending();
        }
    }

    [Test]
    public void ConfigurationProperties_AreExposed()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        Assert.Multiple(() =>
        {
            Assert.That(c.Endpoint.RemoteEndPoint, Is.SameAs(c.RemoteEndPoint));
            Assert.That(c.Endpoint.MessageMaxByteSize, Is.EqualTo(RawEndpointTestContext.DefaultMessageMaxByteSize));
        });
    }

    [Test]
    public void Send_NullBuffer_InvalidMessage()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        var res = c.Endpoint.Send(null!);

        Assert.That(res, Is.EqualTo(SendResult.InvalidMessage));
        Assert.That(c.Endpoint.DeliveredCount, Is.Zero);
    }

    [Test]
    public void Send_ReleasedBuffer_InvalidMessage()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        var buffer = RawTestBuffers.Create(c.Memory);
        buffer.Release();

        var res = c.Endpoint.Send(buffer);

        Assert.That(res, Is.EqualTo(SendResult.InvalidMessage));
        Assert.That(c.Endpoint.DeliveredCount, Is.Zero);
    }

    [Test]
    public void Send_EmptyMessage_Ok()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        var buffer = RawTestBuffers.CreateEmpty(c.Memory);
        var res = c.Endpoint.Send(buffer);

        Assert.That(res, Is.EqualTo(SendResult.Ok));
        DrainPending(c.Driver);
        Assert.That(c.Endpoint.DeliveredCount, Is.EqualTo(1));
        Assert.That(buffer.IsAlive, Is.False);
    }

    [Test]
    public void Send_ExactlyMaxSize_Ok_AndDelivered()
    {
        var buffer = RawTestBuffers.Create(marker: 7);
        int size = buffer.GetDataSize();

        var c = new RawEndpointTestContext(messageMaxByteSize: size);
        c.Activate();

        var res = c.Endpoint.Send(buffer);

        Assert.That(res, Is.EqualTo(SendResult.Ok));
        DrainPending(c.Driver);
        Assert.Multiple(() =>
        {
            Assert.That(c.Endpoint.DeliveredCount, Is.EqualTo(1));
            Assert.That(c.Endpoint.Delivered[0], Is.SameAs(buffer));
            Assert.That(buffer.IsAlive, Is.False);
        });
    }

    [Test]
    public void Send_OverMaxSize_MessageTooBig_ReleasesBuffer()
    {
        var buffer = RawTestBuffers.Create(marker: 7);
        int size = buffer.GetDataSize();

        var c = new RawEndpointTestContext(messageMaxByteSize: size - 1);
        c.Activate();

        var res = c.Endpoint.Send(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(res, Is.EqualTo(SendResult.MessageTooBig));
            Assert.That(buffer.IsAlive, Is.False);
            Assert.That(c.Endpoint.DeliveredCount, Is.Zero);
        });
    }

    [Test]
    public void Send_BeforeStart_NotConnected_ReleasesBuffer()
    {
        var c = new RawEndpointTestContext();
        var buffer = RawTestBuffers.Create(c.Memory);

        var res = c.Endpoint.Send(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(res, Is.EqualTo(SendResult.NotConnected));
            Assert.That(buffer.IsAlive, Is.False);
            Assert.That(c.Endpoint.DeliveredCount, Is.Zero);
        });
    }

    [Test]
    public void Send_AfterStopNow_NotConnected_ReleasesBuffer()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        var reason = new TextFail("unit", "stop");
        c.Endpoint.StopNow(reason);

        var buffer = RawTestBuffers.Create(c.Memory);
        var res = c.Endpoint.Send(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(res, Is.EqualTo(SendResult.NotConnected));
            Assert.That(buffer.IsAlive, Is.False);
            Assert.That(c.Endpoint.DeliveredCount, Is.Zero);
        });
    }

    [Test]
    public void Send_AfterStopGracefully_NotConnected_ReleasesBuffer()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        c.Endpoint.StopGracefully(new TextFail("unit", "stop"));

        var buffer = RawTestBuffers.Create(c.Memory);
        var res = c.Endpoint.Send(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(res, Is.EqualTo(SendResult.NotConnected));
            Assert.That(buffer.IsAlive, Is.False);
            Assert.That(c.Endpoint.DeliveredCount, Is.Zero);
        });
    }

    [Test]
    public void CanSendNowOverride_Reject_ReturnsErrorCode_ReleasesBuffer()
    {
        var c = new RawEndpointTestContext();
        c.Endpoint.CanSendNowOverride = () => SendResult.Error;
        c.Activate();

        var buffer = RawTestBuffers.Create(c.Memory);
        var res = c.Endpoint.Send(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(res, Is.EqualTo(SendResult.Error));
            Assert.That(buffer.IsAlive, Is.False);
            Assert.That(c.Endpoint.DeliveredCount, Is.Zero);
        });
    }

    [Test]
    public void SequentialSends_AreDeliveredInFifoOrder()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        var b0 = RawTestBuffers.Create(c.Memory, marker: 0);
        var b1 = RawTestBuffers.Create(c.Memory, marker: 1);
        var b2 = RawTestBuffers.Create(c.Memory, marker: 2);

        Assert.Multiple(() =>
        {
            Assert.That(c.Endpoint.Send(b0), Is.EqualTo(SendResult.Ok));
            Assert.That(c.Endpoint.Send(b1), Is.EqualTo(SendResult.Ok));
            Assert.That(c.Endpoint.Send(b2), Is.EqualTo(SendResult.Ok));
        });

        DrainPending(c.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(c.Endpoint.DeliveredMarkers, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(b0.IsAlive, Is.False);
            Assert.That(b1.IsAlive, Is.False);
            Assert.That(b2.IsAlive, Is.False);
        });
    }

    [Test]
    public void OkSend_RequestsAnInvocation()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        var buffer = RawTestBuffers.Create(c.Memory);
        Assert.That(c.Endpoint.Send(buffer), Is.EqualTo(SendResult.Ok));
        Assert.That(c.Driver.RequestCount, Is.EqualTo(1));
        Assert.That(c.Driver.HasPendingInvocation, Is.True);

        DrainPending(c.Driver);
        Assert.That(c.Endpoint.DeliveredCount, Is.EqualTo(1));
    }

    [Test]
    public void RejectedSend_RequestsNoInvocation()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        c.Endpoint.StopNow(new TextFail("unit", "stop"));

        var buffer = RawTestBuffers.Create(c.Memory);
        Assert.That(c.Endpoint.Send(buffer), Is.EqualTo(SendResult.NotConnected));
        Assert.That(c.Driver.RequestCount, Is.Zero);
    }

    [Test]
    public void Capacity_ExactFit_Ok_Over_BufferOverflow_ThenFitsAfterDrain()
    {
        var probe = RawTestBuffers.Create(marker: 0);
        int s = probe.GetDataSize();
        probe.Release();

        var c = new RawEndpointTestContext(messageMaxByteSize: s, bufferCapacityBytes: s);
        c.Activate();

        var b1 = RawTestBuffers.Create(c.Memory, marker: 1);
        var b2 = RawTestBuffers.Create(c.Memory, marker: 2);
        var b3 = RawTestBuffers.Create(c.Memory, marker: 3);

        Assert.Multiple(() =>
        {
            Assert.That(c.Endpoint.Send(b1), Is.EqualTo(SendResult.Ok));
            Assert.That(c.Endpoint.Send(b2), Is.EqualTo(SendResult.BufferOverflow));
        });

        Assert.Multiple(() =>
        {
            Assert.That(b2.IsAlive, Is.False);
            Assert.That(c.Endpoint.DeliveredCount, Is.Zero);
        });

        DrainPending(c.Driver);
        Assert.That(c.Endpoint.DeliveredCount, Is.EqualTo(1));

        Assert.That(c.Endpoint.Send(b3), Is.EqualTo(SendResult.Ok));
        DrainPending(c.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(c.Endpoint.DeliveredMarkers, Is.EqualTo(new[] { 1, 3 }));
            Assert.That(b1.IsAlive, Is.False);
            Assert.That(b3.IsAlive, Is.False);
        });
    }

    [Test]
    public void LogicStarted_Activates_OnStartedHookOnce()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        Assert.Multiple(() =>
        {
            Assert.That(c.Endpoint.OnStartedCount, Is.EqualTo(1));
            Assert.That(c.Driver.IsRunning, Is.True);
        });
    }

    [Test]
    public void LogicStarted_AfterStop_Fails_AndKeepsSingleOnStopped()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        var reason = new TextFail("unit", "stop");
        Assert.That(c.Endpoint.StopGracefully(reason), Is.True);

        // Simulate the driver having completed the stop, then try to start again.
        c.Driver.SimulateLogicStopped();
        Assert.That(c.Driver.IsRunning, Is.False);

        var secondDriver = new FakeNonPeriodicDriver();
        Assert.That(secondDriver.Start(c.Endpoint), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));
            Assert.That(c.Handler.LastStopReason, Is.SameAs(reason));
        });
    }

    [Test]
    public void StopNow_DropsQueued_ReleasesBuffers_StopsDriver_DeliversOnStoppedOnce()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        var reason = new TextFail("unit", "abrupt");

        var b1 = RawTestBuffers.Create(c.Memory, marker: 1);
        var b2 = RawTestBuffers.Create(c.Memory, marker: 2);
        Assert.That(c.Endpoint.Send(b1), Is.EqualTo(SendResult.Ok));
        Assert.That(c.Endpoint.Send(b2), Is.EqualTo(SendResult.Ok));
        Assert.That(c.Endpoint.DeliveredCount, Is.Zero);

        c.Endpoint.StopNow(reason);

        Assert.Multiple(() =>
        {
            Assert.That(c.Endpoint.DeliveredCount, Is.Zero);
            Assert.That(b1.IsAlive, Is.False);
            Assert.That(b2.IsAlive, Is.False);
            Assert.That(c.Driver.StopRequestCount, Is.EqualTo(1));
            Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));
            Assert.That(c.Handler.LastStopReason, Is.SameAs(reason));
        });

        // Driver completes afterwards; OnStopped must not be delivered twice.
        c.Driver.SimulateLogicStopped();
        Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));
    }

    [Test]
    public void StopGracefully_FlushesAdmittedSends_ThenStopsDriver()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        var reason = new TextFail("unit", "graceful");

        var b1 = RawTestBuffers.Create(c.Memory, marker: 1);
        var b2 = RawTestBuffers.Create(c.Memory, marker: 2);
        Assert.That(c.Endpoint.Send(b1), Is.EqualTo(SendResult.Ok));
        Assert.That(c.Endpoint.Send(b2), Is.EqualTo(SendResult.Ok));

        Assert.That(c.Endpoint.StopGracefully(reason), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));
            Assert.That(c.Handler.LastStopReason, Is.SameAs(reason));
            Assert.That(c.Endpoint.DeliveredCount, Is.Zero);
            Assert.That(c.Driver.StopRequestCount, Is.Zero);
        });

        // The guaranteed later tick flushes the admitted sends then consumes the marker.
        Assert.That(c.Driver.TickOnce(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(c.Endpoint.DeliveredMarkers, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(b1.IsAlive, Is.False);
            Assert.That(b2.IsAlive, Is.False);
            Assert.That(c.Driver.StopRequestCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void StopGracefully_WithoutLaterTick_LeavesMarkerPending()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        var b1 = RawTestBuffers.Create(c.Memory, marker: 1);
        Assert.That(c.Endpoint.Send(b1), Is.EqualTo(SendResult.Ok));

        // Note: sends are not drained before the graceful stop.
        Assert.That(c.Endpoint.StopGracefully(new TextFail("unit", "graceful")), Is.True);
        Assert.That(c.Driver.StopRequestCount, Is.Zero);

        // No tick is guaranteed to arrive; the marker (and the admitted send) stay queued.
        // A subsequent send is rejected because the stop latch is already set.
        var b2 = RawTestBuffers.Create(c.Memory, marker: 2);
        Assert.That(c.Endpoint.Send(b2), Is.EqualTo(SendResult.NotConnected));

        // Flush for clean teardown.
        c.Driver.TickOnce();
        Assert.That(c.Driver.StopRequestCount, Is.EqualTo(1));
    }

    [Test]
    public void ConcurrentStopCalls_DeliverOnStoppedExactlyOnce()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        var reason = new TextFail("unit", "stop");

        c.Endpoint.StopNow(reason);
        c.Endpoint.StopNow(reason);
        c.Endpoint.StopGracefully(reason);
        c.Endpoint.StopGracefully(reason);

        Assert.Multiple(() =>
        {
            Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));
            Assert.That(c.Handler.LastStopReason, Is.SameAs(reason));
        });

        c.Driver.SimulateLogicStopped();
        Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));
    }

    [Test]
    public void DriverInitiatedStop_DeliversUnknownReason()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        c.Driver.SimulateLogicStopped();

        Assert.Multiple(() =>
        {
            Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));
            Assert.That(c.Handler.LastStopReason, Is.InstanceOf<Unknown>());
            Assert.That(c.Handler.LastStopReason!.Source, Is.EqualTo(c.Transport.Name));
        });

        var buffer = RawTestBuffers.Create(c.Memory);
        Assert.That(c.Endpoint.Send(buffer), Is.EqualTo(SendResult.NotConnected));
    }

    [Test]
    public void Inbound_Active_Delivered_HandlerOwnsBuffer()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        var message = RawTestBuffers.Create(c.Memory, marker: 5);
        c.Endpoint.DeliverInbound(message);

        Assert.Multiple(() =>
        {
            Assert.That(c.Handler.ReceivedCount, Is.EqualTo(1));
            Assert.That(c.Handler.ReceivedBuffers[0], Is.SameAs(message));
            // The handler owns (and releases) the delivered buffer.
            Assert.That(message.IsAlive, Is.False);
            Assert.That(c.Endpoint.DeliveredCount, Is.Zero);
        });
    }

    [Test]
    public void Inbound_PreStart_Dropped_Released()
    {
        var c = new RawEndpointTestContext();

        var message = RawTestBuffers.Create(c.Memory, marker: 5);
        c.Endpoint.DeliverInbound(message);

        Assert.Multiple(() =>
        {
            Assert.That(c.Handler.ReceivedCount, Is.Zero);
            Assert.That(message.IsAlive, Is.False);
        });
    }

    [Test]
    public void Inbound_AfterStop_Dropped_Released()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        c.Endpoint.StopNow(new TextFail("unit", "stop"));

        var message = RawTestBuffers.Create(c.Memory, marker: 5);
        c.Endpoint.DeliverInbound(message);

        Assert.Multiple(() =>
        {
            Assert.That(c.Handler.ReceivedCount, Is.Zero);
            Assert.That(message.IsAlive, Is.False);
        });
    }

    [Test]
    public void Inbound_AfterStop_NoOnReceivedAfterOnStopped()
    {
        var c = new RawEndpointTestContext();
        c.Activate();

        var first = RawTestBuffers.Create(c.Memory, marker: 1);
        c.Endpoint.DeliverInbound(first);

        c.Endpoint.StopGracefully(new TextFail("unit", "stop"));

        var late = RawTestBuffers.Create(c.Memory, marker: 2);
        c.Endpoint.DeliverInbound(late);

        Assert.Multiple(() =>
        {
            Assert.That(c.Handler.ReceivedCount, Is.EqualTo(1));
            Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));
            Assert.That(late.IsAlive, Is.False);
        });
    }

    [Test]
    public void Reentrancy_OnReceived_Sends()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        SendResult? sendResult = null;

        c.Handler.OnReceivedHook = handler =>
        {
            var outbound = RawTestBuffers.Create(marker: 777);
            sendResult = handler.Endpoint!.Send(outbound);
        };

        var inbound = RawTestBuffers.Create(c.Memory, marker: 1);
        c.Endpoint.DeliverInbound(inbound);

        Assert.That(sendResult, Is.EqualTo(SendResult.Ok));
        Assert.That(inbound.IsAlive, Is.False);

        DrainPending(c.Driver);
        Assert.That(c.Endpoint.DeliveredMarkers, Is.EqualTo(new[] { 777 }));
    }

    [Test]
    public void Reentrancy_OnReceived_StopGracefully()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        var stopReason = new TextFail("unit", "from-handler");
        bool? stopResult = null;

        c.Handler.OnReceivedHook = handler =>
        {
            stopResult = handler.Endpoint!.StopGracefully(stopReason);
        };

        var inbound = RawTestBuffers.Create(c.Memory, marker: 1);
        c.Endpoint.DeliverInbound(inbound);

        Assert.Multiple(() =>
        {
            Assert.That(stopResult, Is.True);
            Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));
            Assert.That(c.Handler.LastStopReason, Is.SameAs(stopReason));
            Assert.That(inbound.IsAlive, Is.False);
        });

        var later = RawTestBuffers.Create(c.Memory, marker: 2);
        Assert.That(c.Endpoint.Send(later), Is.EqualTo(SendResult.NotConnected));
    }

    [Test]
    public void Reentrancy_OnStopped_SendRejected_StopIsNoOp()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        SendResult? sendResult = null;
        bool? stopResult = null;

        c.Handler.OnStoppedHook = handler =>
        {
            var buffer = RawTestBuffers.Create(marker: 9);
            sendResult = handler.Endpoint!.Send(buffer);
            stopResult = handler.Endpoint!.StopGracefully(new TextFail("unit", "again"));
        };

        var reason = new TextFail("unit", "first");
        c.Endpoint.StopGracefully(reason);

        Assert.Multiple(() =>
        {
            Assert.That(sendResult, Is.EqualTo(SendResult.NotConnected));
            Assert.That(stopResult, Is.False);
            Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void DoSendRaw_Throwing_ConvertsToStopNowExceptionFail_NoThrowEscapesTick()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        var thrown = new InvalidOperationException("boom");
        c.Endpoint.OnDoSendRaw = _ => throw thrown;

        var buffer = RawTestBuffers.Create(c.Memory, marker: 1);
        Assert.That(c.Endpoint.Send(buffer), Is.EqualTo(SendResult.Ok));

        // The tick must not propagate the DoSendRaw exception.
        Assert.DoesNotThrow(() => c.Driver.TickOnce());

        Assert.Multiple(() =>
        {
            Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));
            Assert.That(c.Handler.LastStopReason, Is.InstanceOf<ExceptionFail>());
            var fail = (ExceptionFail)c.Handler.LastStopReason!;
            Assert.That(fail.Exception, Is.SameAs(thrown));
            Assert.That(c.Driver.StopRequestCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(c.Endpoint.DeliveredCount, Is.Zero);
        });
    }

    [Test]
    public void Handler_OnReceivedThrows_PropagatesToDeliverCaller_NoDeadlock()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        c.Handler.OnReceivedHook = _ => throw new InvalidOperationException("receive");

        var message = RawTestBuffers.Create(c.Memory, marker: 5);

        Assert.Throws<InvalidOperationException>(() => c.Endpoint.DeliverInbound(message));
        // Buffer still released by the handler (its finally), state consistent.
        Assert.That(message.IsAlive, Is.False);

        var buffer = RawTestBuffers.Create(c.Memory);
        Assert.That(c.Endpoint.Send(buffer), Is.EqualTo(SendResult.Ok));
        DrainPending(c.Driver);
        Assert.That(c.Endpoint.DeliveredCount, Is.EqualTo(1));
    }

    [Test]
    public void Handler_OnStoppedThrows_PropagatesToStopCaller_StateConsistent()
    {
        var c = new RawEndpointTestContext();
        c.Activate();
        c.Handler.OnStoppedHook = _ => throw new InvalidOperationException("stopped");

        Assert.Throws<InvalidOperationException>(() => c.Endpoint.StopNow(new TextFail("unit", "stop")));
        Assert.That(c.Handler.StoppedCount, Is.EqualTo(1));

        var buffer = RawTestBuffers.Create(c.Memory);
        Assert.That(c.Endpoint.Send(buffer), Is.EqualTo(SendResult.NotConnected));
    }
}
