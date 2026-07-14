using Actuarius.Memory;

namespace Pontifex.Utils;

public sealed class ZigZagVarIntSerializerTests
{
    private static byte[] WriteIntToArray(int value)
    {
        var sink = new TestByteSink();
        Assert.That(ZigZagVarIntSerializer.WriteInt(value, ref sink), Is.True);
        return sink.ToArray();
    }

    private static int ReadIntFromArray(byte[] data)
    {
        var source = new TestByteSource(data);
        Assert.That(ZigZagVarIntSerializer.ReadInt(ref source, out var value), Is.True);
        return value;
    }

    private static byte[] WriteLongToArray(long value)
    {
        var sink = new TestByteSink();
        Assert.That(ZigZagVarIntSerializer.WriteLong(value, ref sink), Is.True);
        return sink.ToArray();
    }

    private static long ReadLongFromArray(byte[] data)
    {
        var source = new TestByteSource(data);
        Assert.That(ZigZagVarIntSerializer.ReadLong(ref source, out var value), Is.True);
        return value;
    }

    // ---------------------------------------------------------------
    //  GetIntEncodedSize
    // ---------------------------------------------------------------

    [Test]
    public void GetIntEncodedSize_Zero()
    {
        Assert.That(ZigZagVarIntSerializer.GetIntEncodedSize(0), Is.EqualTo(1));
    }

    [Test]
    public void GetIntEncodedSize_One()
    {
        Assert.That(ZigZagVarIntSerializer.GetIntEncodedSize(1), Is.EqualTo(1));
    }

    [Test]
    public void GetIntEncodedSize_MinusOne()
    {
        Assert.That(ZigZagVarIntSerializer.GetIntEncodedSize(-1), Is.EqualTo(1));
    }

    [Test]
    public void GetIntEncodedSize_Boundary63()
    {
        Assert.That(ZigZagVarIntSerializer.GetIntEncodedSize(63), Is.EqualTo(1));
    }

    [Test]
    public void GetIntEncodedSize_Boundary64()
    {
        Assert.That(ZigZagVarIntSerializer.GetIntEncodedSize(64), Is.EqualTo(2));
    }

    [Test]
    public void GetIntEncodedSize_Boundary8191()
    {
        Assert.That(ZigZagVarIntSerializer.GetIntEncodedSize(8191), Is.EqualTo(2));
    }

    [Test]
    public void GetIntEncodedSize_Boundary8192()
    {
        Assert.That(ZigZagVarIntSerializer.GetIntEncodedSize(8192), Is.EqualTo(3));
    }

    [Test]
    public void GetIntEncodedSize_Boundary1048575()
    {
        Assert.That(ZigZagVarIntSerializer.GetIntEncodedSize(1048575), Is.EqualTo(3));
    }

    [Test]
    public void GetIntEncodedSize_Boundary1048576()
    {
        Assert.That(ZigZagVarIntSerializer.GetIntEncodedSize(1048576), Is.EqualTo(4));
    }

    [Test]
    public void GetIntEncodedSize_Boundary134217727()
    {
        Assert.That(ZigZagVarIntSerializer.GetIntEncodedSize(134217727), Is.EqualTo(4));
    }

    [Test]
    public void GetIntEncodedSize_Boundary134217728()
    {
        Assert.That(ZigZagVarIntSerializer.GetIntEncodedSize(134217728), Is.EqualTo(5));
    }

    [Test]
    public void GetIntEncodedSize_MaxValue()
    {
        Assert.That(ZigZagVarIntSerializer.GetIntEncodedSize(int.MaxValue), Is.EqualTo(5));
    }

    [Test]
    public void GetIntEncodedSize_MinValue()
    {
        Assert.That(ZigZagVarIntSerializer.GetIntEncodedSize(int.MinValue), Is.EqualTo(5));
    }

    // ---------------------------------------------------------------
    //  WriteInt / ReadInt  round-trip
    // ---------------------------------------------------------------

    [Test]
    public void RoundTrip_Int_Zero()
    {
        AssertRoundTripInt(0);
    }

    [Test]
    public void RoundTrip_Int_One()
    {
        AssertRoundTripInt(1);
    }

    [Test]
    public void RoundTrip_Int_MinusOne()
    {
        AssertRoundTripInt(-1);
    }

    [Test]
    public void RoundTrip_Int_SmallPositive()
    {
        AssertRoundTripInt(42);
    }

    [Test]
    public void RoundTrip_Int_SmallNegative()
    {
        AssertRoundTripInt(-42);
    }

    [Test]
    public void RoundTrip_Int_Boundary63()
    {
        AssertRoundTripInt(63);
    }

    [Test]
    public void RoundTrip_Int_Boundary64()
    {
        AssertRoundTripInt(64);
    }

    [Test]
    public void RoundTrip_Int_Boundary8191()
    {
        AssertRoundTripInt(8191);
    }

    [Test]
    public void RoundTrip_Int_Boundary8192()
    {
        AssertRoundTripInt(8192);
    }

    [Test]
    public void RoundTrip_Int_Boundary1048575()
    {
        AssertRoundTripInt(1048575);
    }

    [Test]
    public void RoundTrip_Int_Boundary1048576()
    {
        AssertRoundTripInt(1048576);
    }

    [Test]
    public void RoundTrip_Int_Boundary134217727()
    {
        AssertRoundTripInt(134217727);
    }

    [Test]
    public void RoundTrip_Int_Boundary134217728()
    {
        AssertRoundTripInt(134217728);
    }

    [Test]
    public void RoundTrip_Int_MaxValue()
    {
        AssertRoundTripInt(int.MaxValue);
    }

    [Test]
    public void RoundTrip_Int_MinValue()
    {
        AssertRoundTripInt(int.MinValue);
    }

    [Test]
    public void RoundTrip_Int_NegativeBoundary_M64()
    {
        AssertRoundTripInt(-64);
    }

    [Test]
    public void RoundTrip_Int_NegativeBoundary_M65()
    {
        AssertRoundTripInt(-65);
    }

    [Test]
    public void RoundTrip_Int_NegativeBoundary_M8192()
    {
        AssertRoundTripInt(-8192);
    }

    [Test]
    public void RoundTrip_Int_NegativeBoundary_M8193()
    {
        AssertRoundTripInt(-8193);
    }

    [Test]
    public void RoundTrip_Int_RandomValues()
    {
        var rand = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            int value = rand.Next(int.MinValue, int.MaxValue);
            AssertRoundTripInt(value);
        }
    }

    // ---------------------------------------------------------------
    //  WriteLong / ReadLong  round-trip
    // ---------------------------------------------------------------

    [Test]
    public void RoundTrip_Long_Zero()
    {
        AssertRoundTripLong(0L);
    }

    [Test]
    public void RoundTrip_Long_One()
    {
        AssertRoundTripLong(1L);
    }

    [Test]
    public void RoundTrip_Long_MinusOne()
    {
        AssertRoundTripLong(-1L);
    }

    [Test]
    public void RoundTrip_Long_MaxValue()
    {
        AssertRoundTripLong(long.MaxValue);
    }

    [Test]
    public void RoundTrip_Long_MinValue()
    {
        AssertRoundTripLong(long.MinValue);
    }

    [Test]
    public void RoundTrip_Long_SmallValues()
    {
        foreach (var value in new long[] { 42, -42, 127, 128, 16383, 16384 })
            AssertRoundTripLong(value);
    }

    [Test]
    public void RoundTrip_Long_LargeValues()
    {
        foreach (var value in new long[]
        {
            1L << 31,
            (1L << 31) - 1,
            1L << 32,
            (1L << 32) - 1,
            1L << 60,
            (1L << 60) - 1,
        })
            AssertRoundTripLong(value);
    }

    [Test]
    public void RoundTrip_Long_RandomValues()
    {
        var rand = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            long value = ((long)rand.Next(int.MinValue, int.MaxValue) << 32) |
                         (uint)rand.Next(int.MinValue, int.MaxValue);
            AssertRoundTripLong(value);
        }
    }

    // ---------------------------------------------------------------
    //  Encoding byte-level verification
    // ---------------------------------------------------------------

    [Test]
    public void Encode_Zero_IsSingleZeroByte()
    {
        var bytes = WriteIntToArray(0);
        Assert.That(bytes, Is.EqualTo(new byte[] { 0 }));
    }

    [Test]
    public void Encode_One_IsSingleByte()
    {
        var bytes = WriteIntToArray(1);
        Assert.That(bytes, Is.EqualTo(new byte[] { 2 }));
    }

    [Test]
    public void Encode_MinusOne_IsSingleByte()
    {
        var bytes = WriteIntToArray(-1);
        Assert.That(bytes, Is.EqualTo(new byte[] { 1 }));
    }

    [Test]
    public void Encode_64_IsTwoBytes()
    {
        var bytes = WriteIntToArray(64);
        Assert.That(bytes, Is.EqualTo(new byte[] { 0x80, 0x01 }));
    }

    [Test]
    public void Encode_MaxValue_IsFiveBytes()
    {
        var bytes = WriteIntToArray(int.MaxValue);
        Assert.That(bytes.Length, Is.EqualTo(5));
        var back = ReadIntFromArray(bytes);
        Assert.That(back, Is.EqualTo(int.MaxValue));
    }

    // ---------------------------------------------------------------
    //  Failure cases
    // ---------------------------------------------------------------

    [Test]
    public void ReadInt_EmptySource_ReturnsFalse()
    {
        var source = new TestByteSource([]);
        Assert.That(ZigZagVarIntSerializer.ReadInt(ref source, out _), Is.False);
    }

    [Test]
    public void ReadLong_EmptySource_ReturnsFalse()
    {
        var source = new TestByteSource([]);
        Assert.That(ZigZagVarIntSerializer.ReadLong(ref source, out _), Is.False);
    }

    [Test]
    public void ReadInt_TruncatedSource_ReturnsFalse()
    {
        var source = new TestByteSource([0x80]);
        Assert.That(ZigZagVarIntSerializer.ReadInt(ref source, out _), Is.False);
    }

    [Test]
    public void ReadLong_TruncatedSource_ReturnsFalse()
    {
        var source = new TestByteSource([0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80]);
        Assert.That(ZigZagVarIntSerializer.ReadLong(ref source, out _), Is.False);
    }

    // ---------------------------------------------------------------
    //  WriteInt matches WriteLong for int-range values
    // ---------------------------------------------------------------

    [Test]
    public void WriteInt_Matches_WriteLong_ForIntValues()
    {
        var values = new[] { 0, 1, -1, 63, 64, 8191, 8192, int.MaxValue, int.MinValue };
        foreach (var value in values)
        {
            var intBytes = WriteIntToArray(value);
            var longBytes = WriteLongToArray(value);
            Assert.That(intBytes, Is.EqualTo(longBytes),
                $"Mismatch for value {value}");
        }
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    private static void AssertRoundTripInt(int value)
    {
        var bytes = WriteIntToArray(value);
        var decoded = ReadIntFromArray(bytes);
        Assert.That(decoded, Is.EqualTo(value));
    }

    private static void AssertRoundTripLong(long value)
    {
        var bytes = WriteLongToArray(value);
        var source = new TestByteSource(bytes);
        Assert.That(ZigZagVarIntSerializer.ReadLong(ref source, out var decoded), Is.True);
        Assert.That(decoded, Is.EqualTo(value));
    }
}

file sealed class TestByteSink : IByteSink
{
    private readonly List<byte> _buffer = [];

    public byte[] ToArray() => [.. _buffer];

    public bool Put(byte b)
    {
        _buffer.Add(b);
        return true;
    }

    public bool PutMany<TBytes>(TBytes bytes)
        where TBytes : IReadOnlyBytes
    {
        var arr = new byte[bytes.Count];
        bytes.CopyTo(arr, 0, 0, bytes.Count);
        _buffer.AddRange(arr);
        return true;
    }
}

file sealed class TestByteSource : IByteSource
{
    private readonly byte[] _data;
    private int _position;

    public TestByteSource(byte[] data)
    {
        _data = data;
    }

    public bool TryPop(out byte value)
    {
        if (_position < _data.Length)
        {
            value = _data[_position++];
            return true;
        }
        value = 0;
        return false;
    }

    public bool TakeMany(IMultiRefByteArray dst)
    {
        int count = Math.Min(dst.Count, _data.Length - _position);
        for (int i = 0; i < count; i++)
            dst[i] = _data[_position++];
        return count == dst.Count;
    }
}
