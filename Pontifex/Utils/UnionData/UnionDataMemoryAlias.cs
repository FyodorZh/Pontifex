using System;
using System.Runtime.InteropServices;
using Actuarius.Memory;

namespace Pontifex.Utils
{
    [StructLayout(LayoutKind.Explicit)]
    public struct UnionDataMemoryAlias
    {
        [FieldOffset(0)] public bool BoolValue;
        [FieldOffset(0)] public byte ByteValue;
        [FieldOffset(0)] public char CharValue;
        [FieldOffset(0)] public short ShortValue;
        [FieldOffset(0)] public ushort UShortValue;
        [FieldOffset(0)] public int IntValue;
        [FieldOffset(0)] public uint UIntValue;
        [FieldOffset(0)] public long LongValue;
        [FieldOffset(0)] public ulong ULongValue;
        [FieldOffset(0)] public float FloatValue;
        [FieldOffset(0)] public double DoubleValue;
        [FieldOffset(0)] public decimal DecimalValue;
        
        [FieldOffset(0)] public byte Byte0;
        [FieldOffset(1)] public byte Byte1;
        [FieldOffset(2)] public byte Byte2;
        [FieldOffset(3)] public byte Byte3;
        [FieldOffset(4)] public byte Byte4;
        [FieldOffset(5)] public byte Byte5;
        [FieldOffset(6)] public byte Byte6;
        [FieldOffset(7)] public byte Byte7;
        [FieldOffset(8)] public byte Byte8;
        [FieldOffset(9)] public byte Byte9;
        [FieldOffset(10)] public byte Byte10;
        [FieldOffset(11)] public byte Byte11;
        [FieldOffset(12)] public byte Byte12;
        [FieldOffset(13)] public byte Byte13;
        [FieldOffset(14)] public byte Byte14;
        [FieldOffset(15)] public byte Byte15;

        [FieldOffset(8)] private long _highHalf;

        public static implicit operator UnionDataMemoryAlias(bool value) => new UnionDataMemoryAlias() {BoolValue = value};
        public static implicit operator UnionDataMemoryAlias(byte value) => new UnionDataMemoryAlias() {ByteValue = value};
        public static implicit operator UnionDataMemoryAlias(char value) => new UnionDataMemoryAlias() {CharValue = value};
        public static implicit operator UnionDataMemoryAlias(short value) => new UnionDataMemoryAlias() {ShortValue = value};
        public static implicit operator UnionDataMemoryAlias(ushort value) => new UnionDataMemoryAlias() {UShortValue = value};
        public static implicit operator UnionDataMemoryAlias(int value) => new UnionDataMemoryAlias() {IntValue = value};
        public static implicit operator UnionDataMemoryAlias(uint value) => new UnionDataMemoryAlias() {UIntValue = value};
        public static implicit operator UnionDataMemoryAlias(long value) => new UnionDataMemoryAlias() {LongValue = value};
        public static implicit operator UnionDataMemoryAlias(ulong value) => new UnionDataMemoryAlias() {ULongValue = value};
        public static implicit operator UnionDataMemoryAlias(float value) => new UnionDataMemoryAlias() {FloatValue = value};
        public static implicit operator UnionDataMemoryAlias(double value) => new UnionDataMemoryAlias() {DoubleValue = value};
        public static implicit operator UnionDataMemoryAlias(decimal value) => new UnionDataMemoryAlias() {DecimalValue = value};
        
        public byte this[int id]
        {
            get
            {
                switch (id)
                {
                    case 0: return Byte0;
                    case 1: return Byte1;
                    case 2: return Byte2;
                    case 3: return Byte3;
                    case 4: return Byte4;
                    case 5: return Byte5;
                    case 6: return Byte6;
                    case 7: return Byte7;
                    case 8: return Byte8;
                    case 9: return Byte9;
                    case 10: return Byte10;
                    case 11: return Byte11;
                    case 12: return Byte12;
                    case 13: return Byte13;
                    case 14: return Byte14;
                    case 15: return Byte15;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
            set
            {
                switch (id)
                {
                    case 0: Byte0 = value; return;
                    case 1: Byte1 = value; return;
                    case 2: Byte2 = value; return;
                    case 3: Byte3 = value; return;
                    case 4: Byte4 = value; return;
                    case 5: Byte5 = value; return;
                    case 6: Byte6 = value; return;
                    case 7: Byte7 = value; return;
                    case 8: Byte8 = value; return;
                    case 9: Byte9 = value; return;
                    case 10: Byte10 = value; return;
                    case 11: Byte11 = value; return;
                    case 12: Byte12 = value; return;
                    case 13: Byte13 = value; return;
                    case 14: Byte14 = value; return;
                    case 15: Byte15 = value; return;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        public bool Equals1(UnionDataMemoryAlias other) => ByteValue == other.ByteValue;
        public bool Equals2(UnionDataMemoryAlias other) => ShortValue == other.ShortValue;
        public bool Equals4(UnionDataMemoryAlias other) => IntValue == other.IntValue;
        public bool Equals8(UnionDataMemoryAlias other) => LongValue == other.LongValue;
        public bool Equals16(UnionDataMemoryAlias other) => LongValue == other.LongValue && _highHalf == other._highHalf;

        public bool WriteTo1<TByteSink>(ref TByteSink sink)
            where TByteSink : IByteSink
        {
            return sink.Put(Byte0);
        }
        
        public bool WriteTo2<TByteSink>(ref TByteSink sink)
            where TByteSink : IByteSink
        {
            bool res = true;
            res = res && sink.Put(Byte0);
            res = res && sink.Put(Byte1);
            return res;
        }
        
        public bool WriteTo4<TByteSink>(ref TByteSink sink)
            where TByteSink : IByteSink
        {
            bool res = true;
            res = res && sink.Put(Byte0);
            res = res && sink.Put(Byte1);
            res = res && sink.Put(Byte2);
            res = res && sink.Put(Byte3);
            return res;
        }
        
        public bool WriteTo8<TByteSink>(ref TByteSink sink)
            where TByteSink : IByteSink
        {
            bool res = true;
            res = res && sink.Put(Byte0);
            res = res && sink.Put(Byte1);
            res = res && sink.Put(Byte2);
            res = res && sink.Put(Byte3);
            res = res && sink.Put(Byte4);
            res = res && sink.Put(Byte5);
            res = res && sink.Put(Byte6);
            res = res && sink.Put(Byte7);
            return res;
        }
        
        public bool WriteTo16<TByteSink>(ref TByteSink sink)
            where TByteSink : IByteSink
        {
            bool res = true;
            res = res && sink.Put(Byte0);
            res = res && sink.Put(Byte1);
            res = res && sink.Put(Byte2);
            res = res && sink.Put(Byte3);
            res = res && sink.Put(Byte4);
            res = res && sink.Put(Byte5);
            res = res && sink.Put(Byte6);
            res = res && sink.Put(Byte7);
            res = res && sink.Put(Byte8);
            res = res && sink.Put(Byte9);
            res = res && sink.Put(Byte10);
            res = res && sink.Put(Byte11);
            res = res && sink.Put(Byte12);
            res = res && sink.Put(Byte13);
            res = res && sink.Put(Byte14);
            res = res && sink.Put(Byte15);
            return res;
        }
        
        public bool ReadFrom1<TByteSource>(ref TByteSource source)
            where TByteSource : IByteSource
        {
            return source.TryPop(out Byte0);
        }
        
        public bool ReadFrom2<TByteSource>(ref TByteSource source)
            where TByteSource : IByteSource
        {
            bool res = true;
            res = res && source.TryPop(out Byte0);
            res = res && source.TryPop(out Byte1);
            return res;
        }
        
        public bool ReadFrom4<TByteSource>(ref TByteSource source)
            where TByteSource : IByteSource
        {
            bool res = true;
            res = res && source.TryPop(out Byte0);
            res = res && source.TryPop(out Byte1);
            res = res && source.TryPop(out Byte2);
            res = res && source.TryPop(out Byte3);
            return res;
        }
        
        public bool ReadFrom8<TByteSource>(ref TByteSource source)
            where TByteSource : IByteSource
        {
            bool res = true;
            res = res && source.TryPop(out Byte0);
            res = res && source.TryPop(out Byte1);
            res = res && source.TryPop(out Byte2);
            res = res && source.TryPop(out Byte3);
            res = res && source.TryPop(out Byte4);
            res = res && source.TryPop(out Byte5);
            res = res && source.TryPop(out Byte6);
            res = res && source.TryPop(out Byte7);
            return res;
        }
        
        public bool ReadFrom16<TByteSource>(ref TByteSource source)
            where TByteSource : IByteSource
        {
            bool res = true;
            res = res && source.TryPop(out Byte0);
            res = res && source.TryPop(out Byte1);
            res = res && source.TryPop(out Byte2);
            res = res && source.TryPop(out Byte3);
            res = res && source.TryPop(out Byte4);
            res = res && source.TryPop(out Byte5);
            res = res && source.TryPop(out Byte6);
            res = res && source.TryPop(out Byte7);
            res = res && source.TryPop(out Byte8);
            res = res && source.TryPop(out Byte9);
            res = res && source.TryPop(out Byte10);
            res = res && source.TryPop(out Byte11);
            res = res && source.TryPop(out Byte12);
            res = res && source.TryPop(out Byte13);
            res = res && source.TryPop(out Byte14);
            res = res && source.TryPop(out Byte15);
            return res;
        }
    }
}