using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

public abstract class StorageValue<T> : StorageItem
{
    protected T mValue = default(T);
    public T Value
    {
        set { mValue = value; }
        get { return mValue; }
    }

    #region Constructors
    public StorageValue(eStags type) : base(type) { }
    public StorageValue(String name, eStags type) : base(name, type) { }
    public StorageValue(eStags type, T value) : this("", type) { mValue = value; }
    public StorageValue(String name, eStags type, T value) : this(name, type) { mValue = value; }
    #endregion

    override public String asString() { return mValue.ToString(); }
    override public Byte asByte() { return Convert.ToByte(mValue); }
    override public Int32 asInt() { return Convert.ToInt32(mValue); }
    override public UInt32 asUInt() { return Convert.ToUInt32(mValue); }
    override public Int64 asInt64() { return Convert.ToInt64(mValue); }
    override public UInt64 asUInt64() { return Convert.ToUInt64(mValue); }
    override public Double asDouble()
    {
        System.Type type = mValue.GetType();
        if (type == typeof(String))
        {
            Double outVal = 0;
            Double.TryParse(mValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out outVal);
            return outVal;
        }
        return Convert.ToDouble(mValue);
    }

    override public Single asFloat()
    {
        System.Type type = mValue.GetType();
        if (type == typeof(String))
        {
            Single outVal = 0;
            Single.TryParse(mValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out outVal);
            return outVal;
        }
        return Convert.ToSingle(mValue, CultureInfo.InvariantCulture);
    }

    override public Boolean asBool()
    {
        switch (Type)
        {
            case eStags.eSTAG_INT:
            case eStags.eSTAG_UINT:
                return (asUInt() != 0);
            case eStags.eSTAG_STR:
                return StorageItem.ParseBool(asString());
        }
        return false;
    }

    protected abstract T FromString(String val);

    override public void Set(StorageItem from)
    {
        if (!from.Is(mType))
        {
            Log.e("Wrong storage type: {0}", from.Type.ToString());
            return;
        }

        StorageValue<T> from_type = (from as StorageValue<T>);
        if (from_type == null)
        {
            Log.e("Wrong type convertion");
            return;
        }

        this.mValue = from_type.mValue;
    }
    override public Boolean Equals(StorageItem other)
    {
        if (other.Is(mType))
        {
            StorageValue<T> other_type = (other as StorageValue<T>);
            if (other_type == null)
            {
                Log.e("Wrong type convertion");
                return false;
            }
            return IsEqualValues(Value, other_type.Value);
        }
        return false;
    }

    // Функция для сравнения значений хранимого типа данных
    abstract protected Boolean IsEqualValues(T Val1, T Val2);

    override public Boolean Deserialize(XElement input, UInt32 options)
    {
        if (!base.Deserialize(input, options))
        {
            return false;
        }
        mValue = FromString(DeserializeValue(input));
        return true;
    }

    public override bool Deserialize(XmlReader reader, uint options)
    {
        if (!base.Deserialize(reader, options))
        {
            return false;
        }
        mValue = FromString(DeserializeValue(reader));
        return true;
    }

    override public Boolean Serialize(XContainer outParent, UInt32 options)
    {
        if (!base.Serialize(outParent, options))
        {
            return false;
        }
        SerializeValue(outParent, asString());
        return true;
    }
}

public class StorageBool : StorageValue<Boolean>
{
    public const String StorageTag = "bool";

    override public String Datatag()
    {
        return StorageTag;
    }
    #region Constructors
    public StorageBool() : base(eStags.eSTAG_BOOL) { }
    public StorageBool(bool value) : base(eStags.eSTAG_BOOL, value) { }
    public StorageBool(String name) : base(name, eStags.eSTAG_BOOL) { }
    public StorageBool(String name, bool value) : base(name, eStags.eSTAG_BOOL, value) { }
    #endregion

    override protected Boolean IsEqualValues(bool Val1, bool Val2) { return (Val1 == Val2); }
    override public StorageItem Clone()
    {
        return new StorageBool(mName, mValue);
    }
    protected override bool FromString(String val) { return Convert.ToBoolean(val); }

    public override bool asBool()
    {
        return mValue;
    }
}

public class StorageInt : StorageValue<Int32>
{
    public const String StorageTag = "int";

    override public String Datatag()
    {
        return StorageTag;
    }
    #region Constructors
    public StorageInt() : base(eStags.eSTAG_INT) { }
    public StorageInt(Int32 value) : base(eStags.eSTAG_INT, value) { }
    public StorageInt(String name) : base(name, eStags.eSTAG_INT) { }
    public StorageInt(String name, Int32 value) : base(name, eStags.eSTAG_INT, value) { }
    #endregion

    override protected Boolean IsEqualValues(Int32 Val1, Int32 Val2) { return (Val1 == Val2); }
    override public StorageItem Clone()
    {
        return new StorageInt(mName, mValue);
    }
    protected override Int32 FromString(String val) { return Convert.ToInt32(val); }
}

public class StorageByte : StorageValue<Byte>
{
    public const String StorageTag = "byte";

    override public String Datatag()
    {
        return StorageTag;
    }
    #region Constructors
    public StorageByte() : base(eStags.eSTAG_BYTE) { }
    public StorageByte(Byte value) : base(eStags.eSTAG_BYTE, value) { }
    public StorageByte(String name) : base(name, eStags.eSTAG_BYTE) { }
    public StorageByte(String name, Byte value) : base(name, eStags.eSTAG_BYTE, value) { }
    #endregion

    override protected Boolean IsEqualValues(Byte Val1, Byte Val2) { return (Val1 == Val2); }
    override public StorageItem Clone()
    {
        return new StorageInt(mName, mValue);
    }
    protected override Byte FromString(String val) { return Convert.ToByte(val); }
}
public class StorageUInt : StorageValue<UInt32>
{
    public const String StorageTag = "uint";

    #region Constructors
    public StorageUInt() : base(eStags.eSTAG_UINT) { }
    public StorageUInt(UInt32 value) : base(eStags.eSTAG_UINT, value) { }
    public StorageUInt(String name) : base(name, eStags.eSTAG_UINT) { }
    public StorageUInt(String name, UInt32 value) : base(name, eStags.eSTAG_UINT, value) { }
    #endregion

    override public String Datatag()
    {
        return StorageTag;
    }
    override protected Boolean IsEqualValues(UInt32 Val1, UInt32 Val2) { return (Val1 == Val2); }
    override public StorageItem Clone()
    {
        return new StorageUInt(mName, mValue);
    }
    protected override UInt32 FromString(String val) { return Convert.ToUInt32(val); }
}

public class StorageInt64 : StorageValue<Int64>
{
    public const String StorageTag = "int64";

    override public String Datatag()
    {
        return StorageTag;
    }
    #region Constructors
    public StorageInt64() : base(eStags.eSTAG_INT64) { }
    public StorageInt64(Int64 value) : base(eStags.eSTAG_INT64, value) { }
    public StorageInt64(String name) : base(name, eStags.eSTAG_INT64) { }
    public StorageInt64(String name, Int64 value) : base(name, eStags.eSTAG_INT64, value) { }
    #endregion

    override protected Boolean IsEqualValues(Int64 Val1, Int64 Val2) { return (Val1 == Val2); }
    override public StorageItem Clone()
    {
        return new StorageInt64(mName, mValue);
    }
    protected override Int64 FromString(String val) { return Convert.ToInt64(val); }
}

public class StorageUInt64 : StorageValue<UInt64>
{
    public const String StorageTag = "uint64";

    #region Constructors
    public StorageUInt64() : base(eStags.eSTAG_UINT64) { }
    public StorageUInt64(UInt64 value) : base(eStags.eSTAG_UINT64, value) { }
    public StorageUInt64(String name) : base(name, eStags.eSTAG_UINT64) { }
    public StorageUInt64(String name, UInt64 value) : base(name, eStags.eSTAG_UINT64, value) { }
    #endregion

    override public String Datatag()
    {
        return StorageTag;
    }
    override protected Boolean IsEqualValues(UInt64 Val1, UInt64 Val2) { return (Val1 == Val2); }
    override public StorageItem Clone()
    {
        return new StorageUInt64(mName, mValue);
    }
    protected override UInt64 FromString(String val) { return Convert.ToUInt64(val); }
}

public class StorageDouble : StorageValue<Double>
{
    public const String StorageTag = "double";

    #region Constructors
    public StorageDouble() : base(eStags.eSTAG_DOUBLE) { }
    public StorageDouble(Double value) : base(eStags.eSTAG_DOUBLE, value) { }
    public StorageDouble(String name) : base(name, eStags.eSTAG_DOUBLE) { }
    public StorageDouble(String name, Double value) : base(name, eStags.eSTAG_DOUBLE, value) { }
    #endregion

    override public String Datatag()
    {
        return StorageTag;
    }
    override protected Boolean IsEqualValues(Double Val1, Double Val2) { return (Val1 == Val2); }
    override public StorageItem Clone()
    {
        return new StorageDouble(mName, mValue);
    }
    public override String asString() { return Convert.ToString(mValue, CultureInfo.InvariantCulture); }
    protected override Double FromString(String val) { return Double.Parse(val, CultureInfo.InvariantCulture); }
}

public class StorageFloat : StorageValue<Single>
{
    public const String StorageTag = "float";

    #region Constructors
    public StorageFloat() : base(eStags.eSTAG_FLOAT) { }
    public StorageFloat(Single value) : base(eStags.eSTAG_FLOAT, value) { }
    public StorageFloat(String name) : base(name, eStags.eSTAG_FLOAT) { }
    public StorageFloat(String name, Single value) : base(name, eStags.eSTAG_FLOAT, value) { }
    #endregion

    override public String Datatag()
    {
        return StorageTag;
    }
    override protected Boolean IsEqualValues(Single Val1, Single Val2) { return (Val1 == Val2); }
    override public StorageItem Clone()
    {
        return new StorageFloat(mName, mValue);
    }
    public override String asString() { return Convert.ToString(mValue, CultureInfo.InvariantCulture); }
    protected override Single FromString(String val) { return Single.Parse(val, CultureInfo.InvariantCulture); }
}

public class StorageString : StorageValue<String>
{
    public const String StorageTag = "str";

    #region Constructors
    public StorageString() : base(eStags.eSTAG_STR) { }
    public StorageString(String value) : base(eStags.eSTAG_STR, value) { }
    public StorageString(String name, String value) : base(name, eStags.eSTAG_STR, value) { }
    #endregion

    override public String Datatag()
    {
        return StorageTag;
    }
    override public String asString() { return mValue == null ? String.Empty : mValue; }

    override protected Boolean IsEqualValues(String Val1, String Val2) { return (Val1 == Val2); }
    override public StorageItem Clone()
    {
        return new StorageString(mName, mValue);
    }
    protected override String FromString(String val) { return val; }
}

public abstract class CRawStorageDataUser
{
    public CRawStorageDataUser() { }
    public CRawStorageDataUser(StorageBinary file) { file.DeserializeToDataUser(this); }

    public abstract void Serialize(StorageBinary outFile);
    public abstract void Deserialize(StorageBinary inFile);
}

public class StorageBinary : StorageValue<byte[]>
{
    public const String StorageTag = "file";

    #region Constructors
    public StorageBinary() : base(eStags.eSTAG_FILE) { }
    public StorageBinary(String name) : base(name, eStags.eSTAG_FILE) { }
    public StorageBinary(byte[] value) : base(eStags.eSTAG_FILE, value) { }
    public StorageBinary(String name, byte[] value) : base(name, eStags.eSTAG_FILE, value) { }
    public StorageBinary(String name, CRawStorageDataUser dataUser)
        : base(name, eStags.eSTAG_FILE)
    {
        SerializeFromDataUser(dataUser);
    }
    #endregion

    public void SerializeFromDataUser(CRawStorageDataUser dataUser)
    {
        dataUser.Serialize(this);
    }
    public void DeserializeToDataUser(CRawStorageDataUser dataUser)
    {
        dataUser.Deserialize(this);
    }

    override public String asString()
    {
        StringBuilder str = new StringBuilder();
        int length = Size;
        for (int i = 0; i < length; i++)
        {
            str.Append(mValue[i].ToString("X2"));
        }
        return str.ToString();
    }
    override public Int32 asInt() { return 0; }
    override public UInt32 asUInt() { return 0U; }
    override public Double asDouble() { return 0.0; }
    override public Single asFloat() { return 0.0f; }
    override public Boolean asBool() { return false; }

    public Int32 Size
    {
        get
        {
            if (mValue == null)
            {
                return 0;
            }
            return mValue.GetLength(0);
        }
    }
    override public String Datatag()
    {
        return StorageTag;
    }
    override protected Boolean IsEqualValues(byte[] Val1, byte[] Val2)
    {
        int length = Val1.GetLength(0);
        if (length != Val2.GetLength(0))
        {
            return false;
        }
        for (int i = 0; i < length; i++)
        {
            if (Val1[i] != Val2[i])
            {
                return false;
            }
        }
        return true;
    }
    override public StorageItem Clone()
    {
        return new StorageBinary(mName, (mValue.Clone() as byte[]));
    }
    public BinaryReader GetRawReader()
    {
        if (mValue == null)
        {
            return null;
        }
        BinaryReader br = new BinaryReader(new MemoryStream(mValue), Encoding.GetEncoding("ISO-8859-1"));
        return br;
    }
    public static BinaryWriter GetRawWriter()
    {
        BinaryWriter bw = new BinaryWriter(new MemoryStream(), Encoding.GetEncoding("ISO-8859-1"));
        return bw;

    }
    public void SetRawData(BinaryWriter bw)
    {
        if (bw == null)
        {
            Log.e("BinaryWriter is null");
            return;
        }

        MemoryStream ms = (bw.BaseStream as MemoryStream);

        if (ms == null)
        {
            Log.e("Wrong conversion from BinaryWriter to MemoryStream");
            return;
        }

        mValue = ms.ToArray();
    }

    protected override byte[] FromString(String val)
    {
        Int32 size = val.Length;
        if (size == 0)
        {
            return null;
        }
        else if ((size % 2) != 0)
        {
            Log.e("Wrong raw data size: {0}", size.ToString());
            return null;
        }
        size /= 2;

        byte[] raw = new byte[size];

        for (int i = 0; i < size; i++)
        {
            raw[i] = Byte.Parse(val.Substring(2 * i, 2), NumberStyles.AllowHexSpecifier);
        }

        return raw;
    }
}