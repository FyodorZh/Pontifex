using System.Globalization;
using Actuarius.Memory;

namespace Pontifex.Utils;

public sealed class UnionDataListTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    private static UnionDataList CreateList()
    {
        return Memory.CollectablePool.Acquire<UnionDataList>();
    }

    private static IMultiRefByteArray Serialize(UnionDataList list)
    {
        int size = list.GetDataSize();
        var buffer = Memory.ByteArraysPool.Acquire(size);
        Assert.That(list.SerializeTo(buffer), Is.True);
        return buffer;
    }

    private static UnionDataList Deserialize(IMultiRefByteArray buffer)
    {
        var source = new ByteSourceFromArray(buffer);
        var result = CreateList();
        Assert.That(result.Deserialize(ref source, Memory.ByteArraysPool), Is.True);
        return result;
    }

    private static void AssertRoundTrip(UnionDataList original)
    {
        using var _ = original.AsDisposable();

        var buffer = Serialize(original);
        using var _b = buffer.AsDisposable();

        var deserialized = Deserialize(buffer);
        using var _d = deserialized.AsDisposable();

        Assert.That(original.EqualByContent(deserialized), Is.True);
        Assert.That(deserialized.Elements.Count, Is.EqualTo(original.Elements.Count));
    }

    [Test]
    public void EmptyList()
    {
        using var _ = CreateList().AsDisposable();
        var list = CreateList();
        Assert.That(list.Elements.Count, Is.EqualTo(0));
        Assert.That(list.GetDataSize(), Is.EqualTo(2));
        Assert.That(list.PeekFirstType(), Is.EqualTo(UnionDataType.Unknown));
        Assert.That(list.TryPopFirst(out UnionData _), Is.False);
        Assert.That(list.ToString(), Is.EqualTo("[]"));
        list.Release();
    }

    [Test]
    public void EmptyList_SerializationRoundTrip()
    {
        var list = CreateList();
        AssertRoundTrip(list);
    }

    [Test]
    public void Clear_RemovesAllElements()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        list.PutFirst(new UnionData(42));
        list.PutFirst(new UnionData(true));
        Assert.That(list.Elements.Count, Is.EqualTo(2));
        list.Clear();
        Assert.That(list.Elements.Count, Is.EqualTo(0));
        Assert.That(list.ToString(), Is.EqualTo("[]"));
    }

    [Test]
    public void PutFirst_And_PopFirst()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        list.PutFirst(new UnionData(10));
        list.PutFirst(new UnionData(20));
        Assert.That(list.Elements.Count, Is.EqualTo(2));

        var first = list.PopFirst();
        Assert.That(first.Type, Is.EqualTo(UnionDataType.Int));
        Assert.That(first.Alias.IntValue, Is.EqualTo(20));
        Assert.That(list.Elements.Count, Is.EqualTo(1));

        var second = list.PopFirst();
        Assert.That(second.Alias.IntValue, Is.EqualTo(10));
        Assert.That(list.Elements.Count, Is.EqualTo(0));
    }

    [Test]
    public void PutLast_AppendsToEnd()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        list.PutLast(new UnionData(1));
        list.PutLast(new UnionData(2));
        list.PutLast(new UnionData(3));
        Assert.That(list.Elements.Count, Is.EqualTo(3));
        Assert.That(list.PopFirst().Alias.IntValue, Is.EqualTo(1));
        Assert.That(list.PopFirst().Alias.IntValue, Is.EqualTo(2));
        Assert.That(list.PopFirst().Alias.IntValue, Is.EqualTo(3));
    }

    [Test]
    public void PeekFirstType_ReturnsCorrectType()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        Assert.That(list.PeekFirstType(), Is.EqualTo(UnionDataType.Unknown));
        list.PutFirst(new UnionData(3.14));
        Assert.That(list.PeekFirstType(), Is.EqualTo(UnionDataType.Double));
    }

    [Test]
    public void TryPopFirst_ReturnsFalseOnEmpty()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        Assert.That(list.TryPopFirst(out UnionData _), Is.False);
    }

    [Test]
    public void Elements_IsReadOnly()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        list.PutFirst(new UnionData(100));
        Assert.That(list.Elements[0].Alias.IntValue, Is.EqualTo(100));
        Assert.That(list.Elements.Count, Is.EqualTo(1));
    }

    [Test]
    public void GetDataSize_AccumulatesCorrectly()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        Assert.That(list.GetDataSize(), Is.EqualTo(2));
        list.PutFirst(new UnionData(true));
        Assert.That(list.GetDataSize(), Is.EqualTo(2 + 2));
        list.PutFirst(new UnionData(42));
        Assert.That(list.GetDataSize(), Is.EqualTo(2 + 2 + 5));
    }

    [Test]
    public void EqualByContent_ComparesCorrectly()
    {
        var a = CreateList();
        using var _a = a.AsDisposable();
        var b = CreateList();
        using var _b = b.AsDisposable();

        a.PutFirst(new UnionData(1));
        a.PutFirst("hello");
        b.PutFirst(new UnionData(1));
        b.PutFirst("hello");
        Assert.That(a.EqualByContent(b), Is.True);

        b.Clear();
        b.PutFirst(new UnionData(1));
        b.PutFirst("world");
        Assert.That(a.EqualByContent(b), Is.False);
    }

    [Test]
    public void ToString_MultipleElements()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        list.PutFirst(new UnionData(1));
        var str = list.ToString();
        Assert.That(str, Does.StartWith("["));
        Assert.That(str, Does.EndWith("]"));
        Assert.That(str, Does.Contain("Int:1"));
    }

    [Test]
    public void SerializeDeserialize_MultipleElements_MixedTypes()
    {
        var list = CreateList();
        list.PutFirst(new UnionData(42));
        list.PutFirst(new UnionData(true));
        list.PutFirst(new UnionData(3.14));
        list.PutFirst("test");
        AssertRoundTrip(list);
    }

    [Test]
    public void SerializeDeserialize_ManyElements()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        for (int i = 0; i < 100; i++)
            list.PutLast(new UnionData(i));
        AssertRoundTrip(list);
    }

    [Test]
    public void PopFirst_ThrowsOnEmpty()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        Assert.Throws<Exception>(() => list.PopFirst());
    }

    [Test]
    public void UnionData_Bool()
    {
        foreach (var value in new[] { true, false })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.Bool));
            Assert.That(data.Alias.BoolValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"Bool:{value}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_Byte()
    {
        foreach (var value in new byte[] { 0, 1, 255, 128, 42 })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.Byte));
            Assert.That(data.Alias.ByteValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"Byte:{value}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_Char()
    {
        foreach (var value in new[] { 'A', 'z', '0', '\0', '\n', '\xFFFF' })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.Char));
            Assert.That(data.Alias.CharValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"Char:{value}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_Short()
    {
        foreach (var value in new short[] { 0, 1, -1, short.MaxValue, short.MinValue, 10000, -10000 })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.Short));
            Assert.That(data.Alias.ShortValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"Short:{value}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_UShort()
    {
        foreach (var value in new ushort[] { 0, 1, ushort.MaxValue, 1000, 50000 })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.UShort));
            Assert.That(data.Alias.UShortValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"UShort:{value}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_Int()
    {
        foreach (var value in new[] { 0, 1, -1, int.MaxValue, int.MinValue, 1234567890, -1234567890 })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.Int));
            Assert.That(data.Alias.IntValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"Int:{value}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_UInt()
    {
        foreach (var value in new uint[] { 0, 1, uint.MaxValue, 1000, 3000000000 })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.UInt));
            Assert.That(data.Alias.UIntValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"UInt:{value}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_Long()
    {
        foreach (var value in new long[] { 0, 1, -1, long.MaxValue, long.MinValue, 1234567890123456789 })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.Long));
            Assert.That(data.Alias.LongValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"Long:{value}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_ULong()
    {
        foreach (var value in new ulong[] { 0, 1, ulong.MaxValue, 1000, 12345678901234567890 })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.ULong));
            Assert.That(data.Alias.ULongValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"ULong:{value}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_Float()
    {
        foreach (var value in new float[] { 0.0f, -1.5f, 3.40282347E+38f, 1.17549435E-38f, float.Epsilon })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.Float));
            Assert.That(data.Alias.FloatValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"Float:{value.ToString(CultureInfo.InvariantCulture)}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_Float_SpecialValues()
    {
        foreach (var value in new float[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.Float));
            Assert.That(data.Alias.FloatValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"Float:{value.ToString(CultureInfo.InvariantCulture)}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_Double()
    {
        foreach (var value in new double[] { 0.0, -1.5, 1.7976931348623157E+308, 4.94065645841247E-324, double.Epsilon })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.Double));
            Assert.That(data.Alias.DoubleValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"Double:{value.ToString(CultureInfo.InvariantCulture)}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_Double_SpecialValues()
    {
        foreach (var value in new double[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.Double));
            Assert.That(data.Alias.DoubleValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"Double:{value.ToString(CultureInfo.InvariantCulture)}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_Decimal()
    {
        foreach (var value in new decimal[] { 0m, 1m, -1m, decimal.MaxValue, decimal.MinusOne, 3.1415926535897932384626433833m })
        {
            var data = new UnionData(value);
            Assert.That(data.Type, Is.EqualTo(UnionDataType.Decimal));
            Assert.That(data.Alias.DecimalValue, Is.EqualTo(value));
            Assert.That(data.ToString(), Is.EqualTo($"Decimal:{value.ToString(CultureInfo.InvariantCulture)}"));

            var list = CreateList();
            list.PutFirst(data);
            AssertRoundTrip(list);
        }
    }

    [Test]
    public void UnionData_Array_Empty()
    {
        var bytes = new StaticReadOnlyByteArray([]);
        var data = new UnionData(bytes);
        Assert.That(data.Type, Is.EqualTo(UnionDataType.Array));
        Assert.That(data.Bytes, Is.SameAs(bytes));
        Assert.That(data.Bytes!.Count, Is.EqualTo(0));
        Assert.That(data.ToString(), Is.EqualTo("Array:[]"));

        var list = CreateList();
        list.PutFirst(data);
        AssertRoundTrip(list);
        bytes.Release();
    }

    [Test]
    public void UnionData_Array_WithData()
    {
        var rawBytes = new byte[] { 0, 1, 2, 255, 128, 64, 32, 16, 8, 4, 2, 1 };
        var bytes = new StaticReadOnlyByteArray(rawBytes);
        var data = new UnionData(bytes);
        Assert.That(data.Type, Is.EqualTo(UnionDataType.Array));
        Assert.That(data.Bytes, Is.SameAs(bytes));
        Assert.That(data.Bytes!.Count, Is.EqualTo(rawBytes.Length));

        var expectedBytes = "[" + string.Join(",", rawBytes) + "]";
        Assert.That(data.ToString(), Is.EqualTo($"Array:{expectedBytes}"));

        var list = CreateList();
        list.PutFirst(data);
        AssertRoundTrip(list);
        bytes.Release();
    }

    [Test]
    public void UnionData_Array_LargeData_ToStringTruncates()
    {
        var rawBytes = Enumerable.Range(0, 150).Select(i => (byte)i).ToArray();
        var bytes = new StaticReadOnlyByteArray(rawBytes);
        var data = new UnionData(bytes);

        var expectedPrefix = "[" + string.Join(",", rawBytes.Take(100)) + "...]";
        Assert.That(data.ToString(), Is.EqualTo($"Array:{expectedPrefix}"));
        bytes.Release();
    }

    [Test]
    public void UnionData_NullArray()
    {
        var data = new UnionData((IMultiRefReadOnlyByteArray?)null);
        Assert.That(data.Type, Is.EqualTo(UnionDataType.NullArray));
        Assert.That(data.Bytes, Is.Null);
        Assert.That(data.ToString(), Is.EqualTo("NullArray:null"));

        var list = CreateList();
        list.PutFirst(data);
        AssertRoundTrip(list);
    }

    [Test]
    public void UnionData_ToString_AllVariants()
    {
        Assert.That(new UnionData(true).ToString(), Is.EqualTo("Bool:True"));
        Assert.That(new UnionData(false).ToString(), Is.EqualTo("Bool:False"));
        Assert.That(new UnionData((byte)42).ToString(), Is.EqualTo("Byte:42"));
        Assert.That(new UnionData('X').ToString(), Is.EqualTo("Char:X"));
        Assert.That(new UnionData((short)-123).ToString(), Is.EqualTo("Short:-123"));
        Assert.That(new UnionData((ushort)456).ToString(), Is.EqualTo("UShort:456"));
        Assert.That(new UnionData(-789).ToString(), Is.EqualTo("Int:-789"));
        Assert.That(new UnionData(123U).ToString(), Is.EqualTo("UInt:123"));
        Assert.That(new UnionData(-999L).ToString(), Is.EqualTo("Long:-999"));
        Assert.That(new UnionData(999UL).ToString(), Is.EqualTo("ULong:999"));
        Assert.That(new UnionData(3.14f).ToString(), Is.EqualTo("Float:" + 3.14f.ToString(CultureInfo.InvariantCulture)));
        Assert.That(new UnionData(2.718).ToString(), Is.EqualTo("Double:" + 2.718.ToString(CultureInfo.InvariantCulture)));
        Assert.That(new UnionData(1.5m).ToString(), Is.EqualTo("Decimal:" + 1.5m.ToString(CultureInfo.InvariantCulture)));
        Assert.That(new UnionData((IMultiRefReadOnlyByteArray?)null).ToString(), Is.EqualTo("NullArray:null"));
    }

    [Test]
    public void UnionData_Equals_ByValue()
    {
        var a = new UnionData(42);
        var b = new UnionData(42);
        var c = new UnionData(43);

        Assert.That(a.Equals(b), Is.True);
        Assert.That(a.Equals(c), Is.False);
        Assert.That(a.Equals(new UnionData(42.0)), Is.False);
    }

    [Test]
    public void UnionData_Clone_ProducesEqualIndependentCopy()
    {
        var original = new UnionData(12345);
        var clone = original.Clone();
        Assert.That(original.Equals(clone), Is.True);
        Assert.That(clone.Type, Is.EqualTo(UnionDataType.Int));
        Assert.That(clone.Alias.IntValue, Is.EqualTo(12345));
    }

    [Test]
    public void Extension_PutFirst_Primitives()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        list.PutFirst((byte)1);
        list.PutFirst((short)2);
        list.PutFirst(3);
        Assert.That(list.TryPopFirst(out int intVal) ? intVal : 0, Is.EqualTo(3));
        Assert.That(list.TryPopFirst(out short shortVal) ? shortVal : 0, Is.EqualTo(2));
        Assert.That(list.TryPopFirst(out byte byteVal) ? byteVal : 0, Is.EqualTo(1));
    }

    [Test]
    public void Extension_PutFirst_String()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        list.PutFirst("hello");

        Assert.That(list.TryPopFirst(out IMultiRefReadOnlyByteArray? bytes), Is.True);
        Assert.That(bytes, Is.Not.Null);

        var decoded = EncodingUTF8.GetString(bytes);
        Assert.That(decoded, Is.EqualTo("hello"));
        bytes!.Release();
    }

    [Test]
    public void Extension_TryPopFirst_Typed()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        list.PutFirst(new UnionData(true));
        list.PutFirst(new UnionData((byte)200));
        list.PutFirst(new UnionData((short)-5));
        list.PutFirst(new UnionData((ushort)60000));
        list.PutFirst(new UnionData(999));
        list.PutFirst(new UnionData(123456789L));

        Assert.That(list.TryPopFirst(out long longVal) ? longVal : 0, Is.EqualTo(123456789L));
        Assert.That(list.TryPopFirst(out int intVal) ? intVal : 0, Is.EqualTo(999));
        Assert.That(list.TryPopFirst(out ushort ushortVal) ? ushortVal : 0, Is.EqualTo(60000));
        Assert.That(list.TryPopFirst(out short shortVal) ? shortVal : 0, Is.EqualTo(-5));
        Assert.That(list.TryPopFirst(out byte byteVal) ? byteVal : 0, Is.EqualTo(200));
        Assert.That(list.TryPopFirst(out bool boolVal) ? boolVal : false, Is.EqualTo(true));
    }

    [Test]
    public void Extension_TryPopFirst_Typed_WrongType_ReturnsFalse()
    {
        var list = CreateList();
        using var _ = list.AsDisposable();
        list.PutFirst(new UnionData(42));

        Assert.That(list.TryPopFirst(out bool _), Is.False);
        Assert.That(list.TryPopFirst(out byte _), Is.False);
        Assert.That(list.TryPopFirst(out short _), Is.False);
        Assert.That(list.TryPopFirst(out ushort _), Is.False);
        Assert.That(list.TryPopFirst(out long _), Is.False);
        Assert.That(list.TryPopFirst(out IMultiRefReadOnlyByteArray? _), Is.False);
        Assert.That(list.TryPopFirst(out int intVal) ? intVal : 0, Is.EqualTo(42));
    }

    [Test]
    public void Extension_EqualByContent_DifferentLengths()
    {
        var a = CreateList();
        using var _a = a.AsDisposable();
        var b = CreateList();
        using var _b = b.AsDisposable();

        a.PutFirst(new UnionData(1));
        a.PutFirst(new UnionData(2));
        b.PutFirst(new UnionData(1));
        Assert.That(a.EqualByContent(b), Is.False);
    }

    [Test]
    public void CopyFrom_CopiesElements()
    {
        var source = CreateList();
        using var _s = source.AsDisposable();
        source.PutLast(new UnionData(10));
        source.PutLast(new UnionData(20));

        var dest = CreateList();
        using var _d = dest.AsDisposable();
        dest.CopyFrom(source);

        Assert.That(dest.Elements.Count, Is.EqualTo(2));
        Assert.That(dest.PopFirst().Alias.IntValue, Is.EqualTo(10));
        Assert.That(dest.PopFirst().Alias.IntValue, Is.EqualTo(20));
        Assert.That(source.Elements.Count, Is.EqualTo(2));
    }

    [Test]
    public void CopyFrom_ThrowsOnNonEmptyDest()
    {
        var source = CreateList();
        using var _s = source.AsDisposable();
        source.PutFirst(new UnionData(1));

        var dest = CreateList();
        using var _d = dest.AsDisposable();
        dest.PutFirst(new UnionData(2));

        Assert.Throws<Exception>(() => dest.CopyFrom(source));
    }

    [Test]
    public void UnionDataType_DefaultIsUnknown()
    {
        UnionDataType t = default;
        Assert.That(t, Is.EqualTo(UnionDataType.Unknown));
    }
}

file static class EncodingUTF8
{
    public static string GetString(IMultiRefReadOnlyByteArray bytes)
    {
        var chars = new char[bytes.Count];
        for (int i = 0; i < bytes.Count; i++)
            chars[i] = (char)bytes[i];
        return new string(chars);
    }
}
