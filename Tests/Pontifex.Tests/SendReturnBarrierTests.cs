using System;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Tests.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests;

/// <summary>
/// Pins the send-return barrier and the base send-slot reservation accounting used by the RawUnreliable Direct
/// commit window.
/// </summary>
[TestFixture]
public sealed class SendReturnBarrierTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Test]
    public void Wait_WithNoSender_ReturnsImmediately()
    {
        using var barrier = new SendReturnBarrier();

        var wait = Task.Run(() => barrier.Wait());

        Assert.That(wait.Wait(Timeout), Is.True);
    }

    [Test]
    public void Wait_BlocksUntilTheSenderExits()
    {
        using var barrier = new SendReturnBarrier();
        barrier.Enter();

        try
        {
            var wait = Task.Run(() => barrier.Wait());
            Assert.That(wait.Wait(TimeSpan.FromMilliseconds(50)), Is.False,
                "Wait must block while a send is in flight");

            barrier.Exit();
            Assert.That(wait.Wait(Timeout), Is.True);
        }
        finally
        {
            // Exit was already called on the success path; a double exit is not part of the contract.
        }
    }

    [Test]
    public void Wait_DoesNotBlockOnTheCallersOwnReentrantSend()
    {
        using var barrier = new SendReturnBarrier();

        var wait = Task.Run(() =>
        {
            barrier.Enter();
            barrier.Enter();
            try
            {
                barrier.Wait();
                return true;
            }
            finally
            {
                barrier.Exit();
                barrier.Exit();
            }
        });

        Assert.That(wait.Wait(Timeout), Is.True,
            "the caller's own reentrant sends must be excluded from the wait");
        Assert.That(wait.Result, Is.True);
    }

    [Test]
    public void SendSlotReservation_ReleaseOnce_InvokesTheReleaseActionExactlyOnce()
    {
        using var endpoint = new ReservationEndpoint();

        endpoint.ReleaseReservedTwice();

        Assert.That(endpoint.Releases, Is.EqualTo(1));
    }

    [Test]
    public void Send_WhenSlotIsRejected_ReleasesTheBufferAndReturnsBufferOverflow()
    {
        using var endpoint = new ReservationEndpoint { Exhausted = true };
        endpoint.StartAndComplete();
        UnionDataList buffer = TestBuffers.WithByte(MemoryRental.Shared, 1);

        var result = endpoint.Send(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(SendResult.BufferOverflow));
            Assert.That(buffer.IsAlive, Is.False);
            Assert.That(endpoint.Releases, Is.Zero);
        });
    }

    [Test]
    public void Send_WhenSlotIsReserved_ReleasesTheReservationAfterTransmission()
    {
        using var endpoint = new ReservationEndpoint();
        endpoint.StartAndComplete();
        UnionDataList buffer = TestBuffers.WithByte(MemoryRental.Shared, 1);

        var result = endpoint.Send(buffer);
        endpoint.DrainAll();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(SendResult.Ok));
            Assert.That(endpoint.SentCount, Is.EqualTo(1));
            Assert.That(endpoint.Releases, Is.EqualTo(1));
        });
    }

    private sealed class ReservationEndpoint : RawEndpoint, IDisposable
    {
        private ManualSerialActionQueue? _queue;

        public ReservationEndpoint()
            : base("TestTransport", VoidEndPoint.Instance, new RecordingRawHandler(MemoryRental.Shared),
                1024, 1024 * 1024, MemoryRental.Shared, new Logger([]))
        {
        }

        public int Releases { get; private set; }

        public int SentCount { get; private set; }

        public bool Exhausted { get; set; }

        private SendSlotReservation ReserveForTests()
        {
            return CreateSendSlotReservation(() => Releases++);
        }

        public void ReleaseReservedTwice()
        {
            SendSlotReservation reservation = ReserveForTests();
            reservation.ReleaseOnce();
            reservation.ReleaseOnce();
        }

        public void StartAndComplete()
        {
            Start();
            FinishStart(NoOp);
            DrainAll();
        }

        public void DrainAll()
        {
            _queue!.DrainAll();
        }

        protected override ISerialActionQueue SerialQueueConstructor(Action<Exception> onError)
        {
            return _queue = new ManualSerialActionQueue(onError);
        }

        protected override SendSlotReservation? TryReserveSendSlot()
        {
            return Exhausted ? null : CreateSendSlotReservation(() => Releases++);
        }

        protected override void BeginStart()
        {
        }

        protected override void BeginStop(StopReason reason, bool tryBeGentle)
        {
        }

        protected override void DoSendRaw(UnionDataList bufferToSend)
        {
            SentCount++;
            bufferToSend.Release();
        }

        public void Dispose()
        {
        }

        private static void NoOp()
        {
        }
    }
}
