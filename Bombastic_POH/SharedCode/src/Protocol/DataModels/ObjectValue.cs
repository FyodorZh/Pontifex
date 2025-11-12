using Serializer.BinarySerializer;

namespace Shared.Protocol
{
    public sealed class ObjectValueChar : IDataStruct
    {
        public char value;
        public ObjectValueChar() { }
        public ObjectValueChar(char value)
        {
            this.value = value;
        }
        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref value);
            return true;
        }

        public override string ToString()
        {
            return value.ToString();
        }
    }
    public sealed class ObjectValueFloat : IDataStruct
    {
        public float value;
        public ObjectValueFloat()
        {
        }
        public ObjectValueFloat(float value)
        {
            this.value = value;
        }
        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref value);
            return true;
        }

        public override string ToString()
        {
            return value.ToString();
        }
    }
    public sealed class ObjectValueDouble : IDataStruct
    {
        public double value;
        public ObjectValueDouble()
        {
        }
        public ObjectValueDouble(double value)
        {
            this.value = value;
        }
        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref value);
            return true;
        }

        public override string ToString()
        {
            return value.ToString();
        }
    }
    public sealed class ObjectValueShort : IDataStruct
    {
        public short value;
        public ObjectValueShort()
        {
        }
        public ObjectValueShort(short value)
        {
            this.value = value;
        }
        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref value); return true;
        }

        public override string ToString()
        {
            return value.ToString();
        }
    }
    public sealed class ObjectValueByte : IDataStruct
    {
        public byte value;
        public ObjectValueByte()
        {
        }
        public ObjectValueByte(byte value)
        {
            this.value = value;
        }
        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref value);
            return true;
        }

        public override string ToString()
        {
            return value.ToString();
        }
    }
    public sealed class ObjectValueLong : IDataStruct
    {
        public long value;
        public ObjectValueLong()
        {
        }
        public ObjectValueLong(long value)
        {
            this.value = value;
        }
        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref value);
            return true;
        }

        public override string ToString()
        {
            return value.ToString();
        }
    }
    public sealed class ObjectValueBoolean : IDataStruct
    {
        public bool value;
        public ObjectValueBoolean()
        {
        }
        public ObjectValueBoolean(bool value)
        {
            this.value = value;
        }
        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref value);
            return true;
        }

        public override string ToString()
        {
            return value.ToString();
        }
    }
    public sealed class ObjectValueInt : IDataStruct
    {
        public int value;
        public ObjectValueInt() { }
        public ObjectValueInt(int value)
        {
            this.value = value;
        }
        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref value);
            return true;
        }

        public override string ToString()
        {
            return value.ToString();
        }
    }
    public sealed class ObjectValueString : IDataStruct
    {
        public string value;
        public ObjectValueString()
        {
        }
        public ObjectValueString(string value)
        {
            this.value = value;
        }
        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref value);
            return true;
        }

        public override string ToString()
        {
            return value.ToString();
        }
    }
    public sealed class ObjectValueShortArray : IDataStruct
    {
        public short[] value;
        public ObjectValueShortArray()
        {
        }
        public ObjectValueShortArray(short[] value)
        {
            this.value = value;
        }
        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref value); return true;
        }
    }
}
