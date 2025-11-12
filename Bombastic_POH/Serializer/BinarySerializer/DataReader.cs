using System;
using System.Collections.Generic;
using System.Diagnostics;
using Serializer.Factory;

namespace Serializer.BinarySerializer
{
    public class DataReader : DataReader<ManagedReader>
    {
        public DataReader()
        {
        }

        public DataReader(IDataStructFactory factory, IBytesAllocator bytesAllocator)
            :base(factory, bytesAllocator)
        {
        }
    }

    public class DataReader<TReader> : IDataReader
        where TReader : struct, IManagedReader
    {
        public bool isReader { get { return true; } }

        public TReader Reader = new TReader();

        public bool IsEndOfBuffer { get { return Reader.IsEndOfBuffer; } }

        protected IDataStructFactory mFactory;
        private IBytesAllocator mBytesAllocator;
        protected bool mSerializeModelIndex;

        public DataReader()
            : this(new TrivialFactory(), null)
        {
        }

        public DataReader(IDataStructFactory factory, IBytesAllocator bytesAllocator)
        {
            Init(factory, bytesAllocator);
        }

        public void Init(IDataStructFactory factory, IBytesAllocator bytesAllocator)
        {
            mFactory = factory;
            mSerializeModelIndex = mFactory.SerializeModelIndex();
            mBytesAllocator = bytesAllocator;
        }

        #region Helper methods

        public bool GetArray<T>(ref List<T> v)
        {
            int length = Helper.ReadArraySize(ref Reader);
            if (length < 0)
            {
                v = null;
                return false;
            }

            v = new List<T>(length);

            return true;
        }

        public bool GetArray<T>(ref T[] v)
        {
            int length = Helper.ReadArraySize(ref Reader);
            if (length < 0)
            {
                v = null;
                return false;
            }

            v = new T[length];
            return true;
        }

        private string ReadString()
        {
            int length = Helper.ReadArraySize(ref Reader);
            if (length < 0)
            {
                return null;
            }

            if (0 == length)
            {
                return string.Empty;
            }

            var sb = StringBuilderInstance.SB;

            sb.Capacity = Math.Max(sb.Capacity, length);
            for (int i = 0; i < length; ++i)
            {
                sb.Append(Reader.ReadChar());
            }
            return sb.ToString();
        }



        #endregion

        #region IBinarySerializer Members

        public void Add(ref char v) { v = Reader.ReadChar(); }
        public void Add(ref bool v) { v = Reader.ReadBoolean(); }
        public void Add(ref byte v) { v = Reader.ReadByte(); }
        public void Add(ref sbyte v) { v = Reader.ReadSByte(); }
        public void Add(ref short v) { v = Reader.ReadInt16(); }
        public void Add(ref ushort v) { v = Reader.ReadUInt16(); }
        public void Add(ref int v) { v = Reader.ReadInt32(); }
        public void Add(ref uint v) { v = Reader.ReadUInt32(); }
        public void Add(ref long v) { v = Reader.ReadInt64(); }
        public void Add(ref ulong v) { v = Reader.ReadUInt64(); }
        public void Add(ref float v) { v = Reader.ReadSingle(); }
        public void Add(ref double v) { v = Reader.ReadDouble(); }
        public void Add(ref string v) { v = ReadString(); }

        public void Add(ref byte[] v)
        {
            int length = Helper.ReadArraySize(ref Reader);
            if (length < 0)
            {
                v = null;
            }
            else
            {
                v = AllocateByteArray(length);
                Reader.ReadBytes(v);
            }
        }

        public void Add(ref bool[] v)
        {
            if (!GetArray(ref v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                v[i] = Reader.ReadBoolean();
            }
        }

        public void Add(ref short[] v)
        {
            if (!GetArray(ref v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                v[i] = Reader.ReadInt16();
            }
        }

        public void Add(ref ushort[] v)
        {
            if (!GetArray(ref v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                v[i] = Reader.ReadUInt16();
            }
        }

        public void Add(ref int[] v)
        {
            if (!GetArray(ref v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                v[i] = Reader.ReadInt32();
            }
        }

        public void Add(ref uint[] v)
        {
            if (!GetArray(ref v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                v[i] = Reader.ReadUInt32();
            }
        }

        public void Add(ref long[] v)
        {
            if (!GetArray(ref v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                v[i] = Reader.ReadInt64();
            }
        }

        public void Add(ref float[] v)
        {
            if (!GetArray(ref v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                v[i] = Reader.ReadSingle();
            }
        }

        public void Add(ref string[] v)
        {
            if (!GetArray(ref v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                v[i] = ReadString();
            }
        }
        private byte[] AllocateByteArray(int length)
        {
            if (mBytesAllocator == null)
            {
                return new byte[length];
            }

            return mBytesAllocator.Allocate(length);
        }

        public bool Add<T>(ref T v) where T : IDataStruct
        {
            if (StaticTypeInfo<T>.IsValueType)
            {
                // Структуры десериализуем как есть
                v = default(T);
                // ReSharper disable once PossibleNullReferenceException
                return v.Serialize(this);
            }

            if (!mSerializeModelIndex)
            {
                // Безфабричная десериализуем
                byte nullFlag = Reader.ReadByte();
                if (nullFlag == 0)
                {
                    v = default(T); // null
                    return true;
                }
                v = StaticTypeInfo<T>.New();
                if (v == null)
                {
                    Log.e("Can't create target type - {0}", typeof(T).Name);
                    return false;
                }
                return v.Serialize(this);
            }

            short modelIndex = Reader.ReadInt16();
            if (modelIndex < 0)
            {
                v = default(T); // null
                return true;
            }

            object o = mFactory.CreateDataStruct(modelIndex);
            if (o == null)
            {
                Log.e("Can not find type for model index {0}, target type - {1}", modelIndex, typeof(T).Name);
                Debug.Assert(false);
                return false;
            }

            v = (T)o;
            v.Serialize(this);
            return true;
        }

        public bool Add<T>(ref T[] v) where T : IDataStruct
        {
            if (!GetArray(ref v))
            {
                return true;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                T el = default(T);
                if (!Add(ref el))
                {
                    return false;
                }
                v[i] = el;
            }
            return true;
        }

        public bool Add<T>(ref List<T> v) where T : IDataStruct
        {
            if (!GetArray(ref v))
            {
                return true;
            }

            for (int i = 0; i < v.Capacity; ++i)
            {
                T el = default(T);
                if (!Add(ref el))
                {
                    return false;
                }
                v.Add(el);
            }
            return true;
        }

        #endregion

        #region IBinaryReader

        public byte ReadByte()
        {
            return Reader.ReadByte();
        }

        public sbyte ReadSByte()
        {
            return Reader.ReadSByte();
        }

        public bool ReadBoolean()
        {
            return Reader.ReadBoolean();
        }

        public char ReadChar()
        {
            return Reader.ReadChar();
        }

        public short ReadInt16()
        {
            return Reader.ReadInt16();
        }

        public ushort ReadUInt16()
        {
            return Reader.ReadUInt16();
        }

        public int ReadInt32()
        {
            return Reader.ReadInt32();
        }

        public uint ReadUInt32()
        {
            return Reader.ReadUInt32();
        }

        public long ReadInt64()
        {
            return Reader.ReadInt64();
        }

        public ulong ReadUInt64()
        {
            return Reader.ReadUInt64();
        }

        public float ReadSingle()
        {
            return Reader.ReadSingle();
        }

        public double ReadDouble()
        {
            return Reader.ReadDouble();
        }

        public void ReadBytes(byte[] outData)
        {
            Reader.ReadBytes(outData);
        }

        #endregion

        public bool CanPackPrimitives
        {
            get { return Reader.CanPackPrimitives; }
        }
    }
}
