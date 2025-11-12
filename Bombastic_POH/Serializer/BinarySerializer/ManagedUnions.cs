using System.Runtime.InteropServices;

namespace Serializer.BinarySerializer
{
    [StructLayout(LayoutKind.Explicit)]
    public struct ManagedUnions2
    {
        [FieldOffset(0)]
        public char charValue;
        [FieldOffset(0)]
        public short shortValue;
        [FieldOffset(0)]
        public ushort ushortValue;
        [FieldOffset(0)]
        public byte byte0;
        [FieldOffset(1)]
        public byte byte1;

        public byte[] Bytes
        {
            get
            {
                return new[] { byte0, byte1 };
            }
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ManagedUnions4
    {
        [FieldOffset(0)]
        public int intValue;
        [FieldOffset(0)]
        public uint uintValue;
        [FieldOffset(0)]
        public float floatValue;
        [FieldOffset(0)]
        public byte byte0;
        [FieldOffset(1)]
        public byte byte1;
        [FieldOffset(2)]
        public byte byte2;
        [FieldOffset(3)]
        public byte byte3;

        public byte[] Bytes
        {
            get
            {
                return new[] { byte0, byte1, byte2, byte3 };
            }
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ManagedUnions8
    {
        [FieldOffset(0)]
        public long longValue;
        [FieldOffset(0)]
        public ulong ulongValue;
        [FieldOffset(0)]
        public double doubleValue;
        [FieldOffset(0)]
        public byte byte0;
        [FieldOffset(1)]
        public byte byte1;
        [FieldOffset(2)]
        public byte byte2;
        [FieldOffset(3)]
        public byte byte3;
        [FieldOffset(4)]
        public byte byte4;
        [FieldOffset(5)]
        public byte byte5;
        [FieldOffset(6)]
        public byte byte6;
        [FieldOffset(7)]
        public byte byte7;

        public byte[] Bytes
        {
            get
            {
                return new[] { byte0, byte1, byte2, byte3, byte4, byte5, byte6, byte7 };
            }
        }
    }

}
