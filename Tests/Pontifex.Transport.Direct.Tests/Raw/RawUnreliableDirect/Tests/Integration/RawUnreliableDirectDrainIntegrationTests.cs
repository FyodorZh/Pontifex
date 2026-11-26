using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Pontifex.Utils.CheckPointGate;
using Scriba;

namespace Pontifex.Tests.Raw.RawUnreliableDirect;

/// <summary>
/// Pins the mid-drain stop window of the unreliable endpoint: a stop latched while the startup drain is blocked
/// at the pre-delivery gate must release every message that has not begun delivery.
/// </summary>
[TestFixture]
[Category("Direct.RawUnreliable")]
[Category("Direct.RawUnreliable.Integration")]
public sealed class RawUnreliableDirectDrainIntegrationTests
{
    private static readonly ILogger Logger = new Logger([]);

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Dispose();
    }

    [Test]
    public async Task Stop_WhileDrainBlockedAtReceiveGate_DeliversNothingAfterStop()
    {
        var id = "stage2-drain-" + Guid.NewGuid().ToString("N");
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var handler = new GatedDrainHandler();
        Channel? channel = null;

        try
        {
            Assert.That(server.Init(_ => handler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);

            channel = ChannelManager.Instance.Connect(RawUnreliableDirectInfo.TransportName, new StringEndPoint(id),
                new GuidEndPoint(Guid.NewGuid()));
            Assert.That(channel, Is.Not.Null);

            var control = Controls.Get<IRawUnreliableTransportConformanceControl>(server);
            var startedGate = control.BeforeHandlerStartedGate;
            var startedGateReached = startedGate.Arm();

            var firstSend = Task.Run(() => channel!.SendToServer(Message()));

            await startedGateReached.WaitAsync(TimeSpan.FromSeconds(5));

            // The route is published but its endpoint is not started, so these messages are buffered in
            // arrival order behind the trigger.
            Assert.That(channel.SendToServer(Message()), Is.EqualTo(SendResult.Ok));
            Assert.That(channel.SendToServer(Message()), Is.EqualTo(SendResult.Ok));

            startedGate.Reset();

            await handler.GateArmed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await handler.ReceiveGateReached.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(server.Stop(new UserIntention("test", "mid-drain stop")), Is.True);

            handler.ReceiveGate.Reset();

            Assert.That(await firstSend.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(SendResult.Ok));

            Assert.Multiple(() =>
            {
                Assert.That(handler.ReceivedCount, Is.Zero,
                    "a stop latched during the drain must suppress every undelivered message");
                Assert.That(handler.StoppedCount, Is.EqualTo(1),
                    "the start notification completed, so the handler must observe one stop notification");
            });
        }
        finally
        {
            channel?.Dispose();
            server.Stop();
        }
    }

    private static UnionDataList Message()
    {
        var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(1);
        return message;
    }

    private static class Controls
    {
        public static T Get<T>(ITransport transport)
            where T : class, IControl
        {
            var controls = new List<IControl>();
            transport.GetControls(controls, control => control is T);
            return (T)controls.Single();
        }
    }

    private sealed class GatedDrainHandler : IRawUnreliableHandler
    {
        private int _receivedCount;
        private int _stoppedCount;

        public TaskCompletionSource GateArmed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CheckPointWaitResult> ReceiveGateReached { get; private set; } =
            Task.FromResult(CheckPointWaitResult.Released);

        public ICheckPointCtl? ReceiveGate { get; private set; }

        public int ReceivedCount => Volatile.Read(ref _receivedCount);

        public int StoppedCount => Volatile.Read(ref _stoppedCount);

        public void OnStarted(IRawUnreliableEndpoint endpoint)
        {
            var controls = new List<IControl>();
            endpoint.GetControls(controls, control => control is IRawUnreliableEndpointConformanceControl);
            var control = controls.OfType<IRawUnreliableEndpointConformanceControl>().Single();
            ReceiveGate = control.AfterReceivedGate;
            ReceiveGateReached = ReceiveGate.Arm();
            GateArmed.TrySetResult();
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            Interlocked.Increment(ref _receivedCount);
            receivedBuffer.Release();
        }

        public void OnStopped(StopReason reason)
        {
            Interlocked.Increment(ref _stoppedCount);
        }
    }
}
