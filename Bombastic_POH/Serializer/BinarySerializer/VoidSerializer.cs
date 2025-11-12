using System.Collections;
using System.Collections.Generic;

namespace Serializer.BinarySerializer
{
    public class VoidSerializer : IDataReader, IDataWriter
    {
        public static VoidSerializer Instance = new VoidSerializer();

        public bool isReader { get { return true; } }

        public void Add(ref char v) { }
        public void Add(ref bool v) { }
        public void Add(ref byte v) { }
        public void Add(ref sbyte v) { }
        public void Add(ref short v) { }
        public void Add(ref ushort v) { }
        public void Add(ref int v) { }
        public void Add(ref uint v) { }
        public void Add(ref long v) { }
        public void Add(ref ulong v) { }
        public void Add(ref float v) { }
        public void Add(ref double v) { }
        public void Add(ref string v) { }

        public void Add(ref byte[] v) { }
        public void Add(ref bool[] v) { }
        public void Add(ref short[] v) { }
        public void Add(ref ushort[] v) { }
        public void Add(ref int[] v) { }
        public void Add(ref uint[] v) { }
        public void Add(ref long[] v) { }
        public void Add(ref float[] v) { }
        public void Add(ref string[] v) { }

        public bool Add<T>(ref T v) where T : IDataStruct
        {
            return true;
        }

        public bool Add<T>(ref T[] v) where T : IDataStruct
        {
            return true;
        }

        public bool Add<T>(ref List<T> v) where T : IDataStruct
        {
            return true;
        }

        public bool AddStruct<T>(ref List<T> v) where T : struct, IDataStruct
        {
            return true;
        }

        public bool GetArray<T>(ref List<T> v)
        {
            return v != null && v.Count > 0;
        }

        public bool GetArray<T>(ref T[] v)
        {
            return v != null && v.Length > 0;
        }

        public bool PrepareWriteArray(ICollection v)
        {
            return true;
        }

        public bool PrepareWriteArray(int count)
        {
            return true;
        }

        public void AddBytes(byte[] bytes, int @from, int count)
        {
        }

        public byte ReadByte()
        {
            return 0;
        }

        public sbyte ReadSByte()
        {
            return 0;
        }

        public bool ReadBoolean()
        {
            return false;
        }

        public char ReadChar()
        {
            return ' ';
        }

        public short ReadInt16()
        {
            return 0;
        }

        public ushort ReadUInt16()
        {
            return 0;
        }

        public int ReadInt32()
        {
            return 0;
        }

        public uint ReadUInt32()
        {
            return 0;
        }

        public long ReadInt64()
        {
            return 0;
        }

        public ulong ReadUInt64()
        {
            return 0;
        }

        public float ReadSingle()
        {
            return 0;
        }

        public double ReadDouble()
        {
            return 0;
        }

        public void ReadBytes(byte[] outData)
        {
        }

        public void AddByte(byte v)
        {
        }

        public void AddSByte(sbyte v)
        {
        }

        public void AddBoolean(bool v)
        {
        }

        public void AddChar(char v)
        {
        }

        public void AddInt16(short v)
        {
        }

        public void AddUInt16(ushort v)
        {
        }

        public void AddInt32(int v)
        {
        }

        public void AddUInt32(uint v)
        {
        }

        public void AddInt64(long v)
        {
        }

        public void AddUInt64(ulong v)
        {
        }

        public void AddSingle(float v)
        {
        }

        public void AddDouble(double v)
        {
        }

        public void AddBytes(byte[] bytes)
        {
        }

        public bool CanPackPrimitives
        {
            get { return false; }
        }
    }
}
