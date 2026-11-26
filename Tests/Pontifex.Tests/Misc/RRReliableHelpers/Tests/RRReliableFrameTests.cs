using System;
using Actuarius.Memory;
using Pontifex.RR.Reliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Misc.RRReliableHelpers;

[TestFixture]
[Category("RRReliableHelpers")]
public sealed class RRReliableFrameTests
{
    [Test]
    public void RequestFrame_RoundTripsHeaderAndPayload()
    {
        UnionDataList frame = Payload(1, 2, 3);

        try
        {
            int payloadSize = frame.GetDataSize();

            RRReliableFrame.EncodeRequest(frame, 4242);

            Assert.That(frame.GetDataSize(),
                Is.EqualTo(payloadSize + RRReliableFrame.RequestOverheadBytes));
            Assert.That(RRReliableFrame.TryDecode(frame, out RRReliableFrameHeader header), Is.True);
            Assert.That(header.FrameType, Is.EqualTo(RRReliableFrameType.Request));
            Assert.That(header.RequestId, Is.EqualTo((ushort)4242));
            Assert.That(header.Ordinal, Is.Zero);
            Assert.That(frame.Elements.Count, Is.EqualTo(3));
        }
        finally
        {
            frame.Release();
        }
    }

    [Test]
    public void ResponseFrame_RoundTripsOrdinalAndPayload()
    {
        UnionDataList frame = Payload(7);

        try
        {
            RRReliableFrame.EncodeResponse(frame, 9, 11);

            Assert.That(RRReliableFrame.TryDecode(frame, out RRReliableFrameHeader header), Is.True);
            Assert.That(header.FrameType, Is.EqualTo(RRReliableFrameType.Response));
            Assert.That(header.RequestId, Is.EqualTo((ushort)9));
            Assert.That(header.Ordinal, Is.EqualTo(11u));
            Assert.That(frame.Elements.Count, Is.EqualTo(1));
            Assert.That(frame.Elements[0].Alias.IntValue, Is.EqualTo(7));
        }
        finally
        {
            frame.Release();
        }
    }

    [Test]
    public void ResponseFrame_RoundTripsUIntMaxOrdinal()
    {
        UnionDataList frame = Payload(7);

        try
        {
            RRReliableFrame.EncodeResponse(frame, 1, uint.MaxValue);

            Assert.That(RRReliableFrame.TryDecode(frame, out RRReliableFrameHeader header), Is.True);
            Assert.That(header.Ordinal, Is.EqualTo(uint.MaxValue));
        }
        finally
        {
            frame.Release();
        }
    }

    [TestCase(RRReliableCompletionType.Normal)]
    [TestCase(RRReliableCompletionType.Canceled)]
    [TestCase(RRReliableCompletionType.Rejected)]
    [TestCase(RRReliableCompletionType.Failed)]
    public void FinishFrame_RoundTripsEveryCompletion(RRReliableCompletionType completion)
    {
        UnionDataList frame = NewFrame();

        try
        {
            RRReliableFrame.EncodeFinish(frame, 77, completion);

            Assert.That(RRReliableFrame.TryDecode(frame, out RRReliableFrameHeader header), Is.True);
            Assert.That(header.FrameType, Is.EqualTo(RRReliableFrameType.Finish));
            Assert.That(header.RequestId, Is.EqualTo((ushort)77));
            Assert.That(header.Completion, Is.EqualTo(completion));
        }
        finally
        {
            frame.Release();
        }
    }

    [Test]
    public void CancelFrame_RoundTripsHeader()
    {
        UnionDataList frame = NewFrame();

        try
        {
            RRReliableFrame.EncodeCancel(frame, 512);

            Assert.That(RRReliableFrame.TryDecode(frame, out RRReliableFrameHeader header), Is.True);
            Assert.That(header.FrameType, Is.EqualTo(RRReliableFrameType.Cancel));
            Assert.That(header.RequestId, Is.EqualTo((ushort)512));
            Assert.That(frame.Elements.Count, Is.Zero);
        }
        finally
        {
            frame.Release();
        }
    }

    [Test]
    public void Decode_KeepsTheFrameAlive()
    {
        UnionDataList frame = Payload(1);

        try
        {
            RRReliableFrame.EncodeRequest(frame, 1);

            Assert.That(RRReliableFrame.TryDecode(frame, out _), Is.True);
            Assert.That(frame.IsAlive, Is.True);
        }
        finally
        {
            frame.Release();
        }
    }

    [Test]
    public void UnknownFrameType_IsRejected()
    {
        UnionDataList frame = NewFrame();

        try
        {
            frame.PutFirst((byte)99);

            Assert.That(RRReliableFrame.TryDecode(frame, out _), Is.False);
        }
        finally
        {
            frame.Release();
        }
    }

    [Test]
    public void MissingRequestId_IsRejected()
    {
        UnionDataList frame = NewFrame();

        try
        {
            frame.PutFirst((byte)RRReliableFrameType.Request);

            Assert.That(RRReliableFrame.TryDecode(frame, out _), Is.False);
        }
        finally
        {
            frame.Release();
        }
    }

    [Test]
    public void ResponseWithoutOrdinal_IsRejected()
    {
        UnionDataList frame = NewFrame();

        try
        {
            frame.PutFirst((ushort)1);
            frame.PutFirst((byte)RRReliableFrameType.Response);

            Assert.That(RRReliableFrame.TryDecode(frame, out _), Is.False);
        }
        finally
        {
            frame.Release();
        }
    }

    [Test]
    public void ZeroOrdinal_IsRejected()
    {
        UnionDataList frame = NewFrame();

        try
        {
            frame.PutFirst(new UnionData(0));
            frame.PutFirst((ushort)1);
            frame.PutFirst((byte)RRReliableFrameType.Response);

            Assert.That(RRReliableFrame.TryDecode(frame, out _), Is.False);
        }
        finally
        {
            frame.Release();
        }
    }

    [Test]
    public void UnknownCompletion_IsRejected()
    {
        UnionDataList frame = NewFrame();

        try
        {
            frame.PutFirst((byte)99);
            frame.PutFirst((ushort)1);
            frame.PutFirst((byte)RRReliableFrameType.Finish);

            Assert.That(RRReliableFrame.TryDecode(frame, out _), Is.False);
        }
        finally
        {
            frame.Release();
        }
    }

    [Test]
    public void HeaderConstants_MatchTheSerializedOverhead()
    {
        UnionDataList empty = NewFrame();
        int emptySize = empty.GetDataSize();
        empty.Release();

        UnionDataList request = new();
        UnionDataList response = new();
        UnionDataList finish = new();
        UnionDataList cancel = new();

        try
        {
            RRReliableFrame.EncodeRequest(request, 1);
            RRReliableFrame.EncodeResponse(response, 1, 1);
            RRReliableFrame.EncodeFinish(finish, 1, RRReliableCompletionType.Normal);
            RRReliableFrame.EncodeCancel(cancel, 1);

            Assert.That(request.GetDataSize() - emptySize,
                Is.EqualTo(RRReliableFrame.RequestOverheadBytes));
            Assert.That(response.GetDataSize() - emptySize,
                Is.EqualTo(RRReliableFrame.ResponseOverheadBytes));
            Assert.That(finish.GetDataSize() - emptySize,
                Is.EqualTo(RRReliableFrame.FinishOverheadBytes));
            Assert.That(cancel.GetDataSize() - emptySize,
                Is.EqualTo(RRReliableFrame.CancelOverheadBytes));
            Assert.That(RRReliableFrame.MaxOverheadBytes,
                Is.EqualTo(RRReliableFrame.ResponseOverheadBytes));
        }
        finally
        {
            request.Release();
            response.Release();
            finish.Release();
            cancel.Release();
        }
    }

    [Test]
    public void MaximumHeader_AccommodatesTheCountPrefixGrowth()
    {
        // Sixty-three payload elements need a one-byte list-count prefix. Prepending the three
        // response header elements grows the count to sixty-six, whose prefix needs two bytes. The
        // transport adds one spare byte to FrameMaxByteSize for exactly this growth.
        UnionDataList frame = Payload(Enumerable.Range(0, 63).Select(_ => (byte)1).ToArray());

        try
        {
            int payloadSize = frame.GetDataSize();

            RRReliableFrame.EncodeResponse(frame, 1, 1);

            Assert.That(frame.GetDataSize(),
                Is.EqualTo(payloadSize + RRReliableFrame.MaxOverheadBytes + 1));
        }
        finally
        {
            frame.Release();
        }
    }

    [Test]
    public void EncodeRequest_WithNullFrame_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RRReliableFrame.EncodeRequest(null!, 1));
    }

    [Test]
    public void EncodeResponse_WithNullFrame_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RRReliableFrame.EncodeResponse(null!, 1, 1));
    }

    [Test]
    public void EncodeResponse_WithZeroOrdinal_Throws()
    {
        UnionDataList frame = NewFrame();

        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => RRReliableFrame.EncodeResponse(frame, 1, 0));
        }
        finally
        {
            frame.Release();
        }
    }

    [Test]
    public void Decode_WithNullFrame_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => RRReliableFrame.TryDecode(null!, out _));
    }

    private static UnionDataList Payload(params byte[] values)
    {
        UnionDataList frame = NewFrame();

        foreach (byte value in values)
        {
            frame.PutLast(new UnionData(value));
        }

        return frame;
    }

    private static UnionDataList NewFrame()
    {
        return MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
    }
}
