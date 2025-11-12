using System;
using System.Collections.Generic;
using System.Diagnostics;
using Serializer.Factory;

namespace Serializer.BinarySerializer
{
    public class DataWriter : DataWriter<ManagedWriter<DefaultAllocator>>
    {
        public DataWriter(IDataStructFactory factory)
            : base(ManagedWriter<DefaultAllocator>.Construct(new DefaultAllocator()), factory)
        {
        }
    }

    public class DataWriter<TWriter> : IDataWriter
        where TWriter : struct, IManagedWriter
    {
        public bool isReader { get { return false; } }

        /// <summary>
        /// Декоратор для сериализации элементарных типов данных
        /// </summary>
        public TWriter Writer;

        private IDataStructFactory mFactory;
        protected bool mSerializeModelIndex;

        public DataWriter(TWriter writer)
        {
            Writer = writer;
        }

        public DataWriter(TWriter writer, IDataStructFactory factory)
            : this(writer)
        {
            SetFactory(factory);
        }

        public void SetFactory(IDataStructFactory factory)
        {
            mFactory = factory;
            mSerializeModelIndex = mFactory.SerializeModelIndex();
        }

        public void Reset()
        {
            Writer.Clear();
        }

        #region Helper methods

        public bool PrepareWriteArray(System.Collections.ICollection v)
        {
            return PrepareWriteArray(v != null ? v.Count : -1);
        }

        public bool PrepareWriteArray(int count)
        {
            if (count > Helper.MaxArrayLength)
            {
                Log.e("Can't serialize data - array is too long ({0} elements)", count);
                return false;
            }

            Helper.WriteArraySize(ref Writer, count);
            return count >= 0;
        }

        protected void WriteString(string v)
        {
            if (null == v)
            {
                Helper.WriteArraySize(ref Writer, -1);
                return;
            }

            if (v.Length > Helper.MaxArrayLength)
            {
                Log.e("Can't serialize data - string is too long ({0} chars)", v.Length);
                return;
            }

            if (Helper.WriteArraySize(ref Writer, v.Length))
            {
                for (int i = 0; i < v.Length; ++i)
                {
                    Writer.AddChar(v[i]);
                }
            }
        }

        private bool WriteDataStructCollection<T>(List<T> v) where T : IDataStruct
        {
            if (!PrepareWriteArray(v))
            {
                return true;
            }

            foreach (T data in v)
            {
                T tmp = data;
                if (!Add(ref tmp))
                {
                    return false;
                }
            }
            return true;
        }

        private bool WriteDataStructCollection<T>(T[] v) where T : IDataStruct
        {
            if (!PrepareWriteArray(v))
            {
                return true;
            }

            for (int i = 0, c = v.Length; i < c; ++i)
            {
                if (!Add(ref v[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private bool WriteDataStruct<T>(T v) where T : IDataStruct
        {
            if (StaticTypeInfo<T>.IsValueType)
            {
                // Структуры сериализуем как есть
                return v.Serialize(this);
            }

            if (!mSerializeModelIndex)
            {
                // Безфабричная сериализация
                if (v == null)
                {
                    Writer.AddByte(0);
                    return true;
                }
                Writer.AddByte(1);
                return v.Serialize(this);
            }

            if (v != null)
            {
                ITypeProvider typeProvider = v as ITypeProvider;
                Type modelType = typeProvider != null ? typeProvider.GetType() : v.GetType();

                short modelIndex = mFactory.GetIndexForModel(modelType);
                if (modelIndex < 0)
                {
                    Log.e("Can't serialize data - {0} has no model index", v.GetType());
                    Debug.Assert(false);
                    return false;
                }

                // Фабричная сериализация ненулевой модельки
                Writer.AddInt16(modelIndex);
                return v.Serialize(this);
            }

            // Фабричная сериализация нулевой модели
            Writer.AddInt16(-1);
            return true;
        }

        #endregion

        #region IBinarySerializer Members

        public void Add(ref char v) { Writer.AddChar(v); }
        public void Add(ref bool v) { Writer.AddBoolean(v); }
        public void Add(ref byte v) { Writer.AddByte(v); }
        public void Add(ref sbyte v) { Writer.AddSByte(v); }
        public void Add(ref short v) { Writer.AddInt16(v); }
        public void Add(ref ushort v) { Writer.AddUInt16(v); }
        public void Add(ref int v) { Writer.AddInt32(v); }
        public void Add(ref uint v) { Writer.AddUInt32(v); }
        public void Add(ref long v) { Writer.AddInt64(v); }
        public void Add(ref ulong v) { Writer.AddUInt64(v); }
        public void Add(ref float v) { Writer.AddSingle(v); }
        public void Add(ref double v) { Writer.AddDouble(v); }
        public void Add(ref string v) { WriteString(v); }

        public void Add(ref byte[] v)
        {
            if (!PrepareWriteArray(v))
            {
                return;
            }
            Writer.AddBytes(v);
        }

        public void Add(ref bool[] v)
        {
            if (!PrepareWriteArray(v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                var tmp = v[i];
                Add(ref tmp);
            }
        }

        public void Add(ref short[] v)
        {
            if (!PrepareWriteArray(v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                var tmp = v[i];
                Add(ref tmp);
            }
        }

        public void Add(ref ushort[] v)
        {
            if (!PrepareWriteArray(v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                var tmp = v[i];
                Add(ref tmp);
            }
        }

        public void Add(ref int[] v)
        {
            if (!PrepareWriteArray(v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                var tmp = v[i];
                Add(ref tmp);
            }
        }

        public void Add(ref uint[] v)
        {
            if (!PrepareWriteArray(v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                var tmp = v[i];
                Add(ref tmp);
            }
        }

        public void Add(ref long[] v)
        {
            if (!PrepareWriteArray(v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                var tmp = v[i];
                Add(ref tmp);
            }
        }

        public void Add(ref float[] v)
        {
            if (!PrepareWriteArray(v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                var tmp = v[i];
                Add(ref tmp);
            }
        }

        public void Add(ref string[] v)
        {
            if (!PrepareWriteArray(v))
            {
                return;
            }

            for (int i = 0; i < v.Length; ++i)
            {
                var tmp = v[i];
                Add(ref tmp);
            }
        }

        public bool Add<T>(ref T v) where T : IDataStruct { return WriteDataStruct<T>(v); }
        public bool Add<T>(ref T[] v) where T : IDataStruct { return WriteDataStructCollection<T>(v); }
        public bool Add<T>(ref List<T> v) where T : IDataStruct { return WriteDataStructCollection<T>(v); }

        #endregion

        #region IBinaryWriter

        public void AddByte(byte v)
        {
            Writer.AddByte(v);
        }

        public void AddSByte(sbyte v)
        {
            Writer.AddSByte(v);
        }

        public void AddBoolean(bool v)
        {
            Writer.AddBoolean(v);
        }

        public void AddChar(char v)
        {
            Writer.AddChar(v);
        }

        public void AddInt16(short v)
        {
            Writer.AddInt16(v);
        }

        public void AddUInt16(ushort v)
        {
            Writer.AddUInt16(v);
        }

        public void AddInt32(int v)
        {
            Writer.AddInt32(v);
        }

        public void AddUInt32(uint v)
        {
            Writer.AddUInt32(v);
        }

        public void AddInt64(long v)
        {
            Writer.AddInt64(v);
        }

        public void AddUInt64(ulong v)
        {
            Writer.AddUInt64(v);
        }

        public void AddSingle(float v)
        {
            Writer.AddSingle(v);
        }

        public void AddDouble(double v)
        {
            Writer.AddDouble(v);
        }

        public void AddBytes(byte[] bytes)
        {
            Writer.AddBytes(bytes);
        }

        public void AddBytes(byte[] bytes, int from, int count)
        {
            Writer.AddBytes(bytes, from, count);
        }

        #endregion

        public bool CanPackPrimitives
        {
            get { return Writer.CanPackPrimitives; }
        }
    }
}
