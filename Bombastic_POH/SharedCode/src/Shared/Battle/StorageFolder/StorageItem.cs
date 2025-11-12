using System;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

public enum eStags
{
    eSTAG_UNKNOWN = -1,
    eSTAG_FILE = 0,
    eSTAG_FOLDER = 1,
    eSTAG_STR = 2,
    eSTAG_BOOL = 3,
    eSTAG_BYTE = 4,
    eSTAG_INT = 5,
    eSTAG_UINT = 6,
    eSTAG_FLOAT = 7,
    eSTAG_DOUBLE = 8,
    eSTAG_INT64 = 9,
    eSTAG_UINT64 = 10,


    eSTAG_MAX
}

[DebuggerDisplay("{DebuggerDisplay}")]
public abstract class StorageItem
{
    #region Creation functions
    public static StorageItem Build(string tag)
    {
        eStags eTag = eStags.eSTAG_UNKNOWN;

        if (tag == StorageBinary.StorageTag)
            eTag = eStags.eSTAG_FILE;
        else if (tag == StorageFolder.StorageTag)
            eTag = eStags.eSTAG_FOLDER;
        else if (tag == StorageString.StorageTag)
            eTag = eStags.eSTAG_STR;
        else if (tag == StorageBool.StorageTag)
            eTag = eStags.eSTAG_BOOL;
        else if (tag == StorageByte.StorageTag)
            eTag = eStags.eSTAG_BYTE;
        else if (tag == StorageInt.StorageTag)
            eTag = eStags.eSTAG_INT;
        else if (tag == StorageUInt.StorageTag)
            eTag = eStags.eSTAG_UINT;
        else if (tag == StorageFloat.StorageTag)
            eTag = eStags.eSTAG_FLOAT;
        else if (tag == StorageDouble.StorageTag)
            eTag = eStags.eSTAG_DOUBLE;
        else if (tag == StorageInt64.StorageTag)
            eTag = eStags.eSTAG_INT64;
        else if (tag == StorageUInt64.StorageTag)
            eTag = eStags.eSTAG_UINT64;
        else if (tag == "#comment")
        {
            return null;
        }
        else
        {
            Log.e("Unknown storage tag: {0}", tag);
            return null;
        }
        return Build(eTag);
    }

    public static StorageItem Build(eStags tag)
    {
        switch (tag)
        {
            case eStags.eSTAG_FILE:
                return new StorageBinary();
            case eStags.eSTAG_FOLDER:
                return new StorageFolder();
            case eStags.eSTAG_STR:
                return new StorageString();
            case eStags.eSTAG_BYTE:
                return new StorageByte();
            case eStags.eSTAG_BOOL:
                return new StorageBool();
            case eStags.eSTAG_INT:
                return new StorageInt();
            case eStags.eSTAG_UINT:
                return new StorageUInt();
            case eStags.eSTAG_FLOAT:
                return new StorageFloat();
            case eStags.eSTAG_DOUBLE:
                return new StorageDouble();
            case eStags.eSTAG_INT64:
                return new StorageInt64();
            case eStags.eSTAG_UINT64:
                return new StorageUInt64();
        }

        Log.e("Unknown storage tag: {0}", tag.ToString());

        return null;
    }
    #endregion

    private string DebuggerDisplay
    {
        get { return string.Format("{0} - {1} ({2})", mName, asString(), Datatag()); }
    }

    public override string ToString()
    {
        return DebuggerDisplay;
    }

    public static bool ParseBool(string value)
    {
        value = value.ToLower();
        if (value == "1")
        {
            return true;
        }
        else if (value == "0")
        {
            return false;
        }
        else if (value == "true")
        {
            return true;
        }
        else if (value == "on")
        {
            return true;
        }
        else if (value == "enable")
        {
            return true;
        }

        return false;
    }

    protected string mName;
    public string Name
    {
        set { mName = value; }
        get { return mName; }
    }

    protected eStags mType = eStags.eSTAG_UNKNOWN;
    public eStags Type { get { return mType; } }

    public virtual string Datatag()
    {
        Log.e("Unknown storage tag: {0}", mType.ToString());

        return "";
    }

    #region Constructors
    protected StorageItem() { }
    protected StorageItem(string name)
    {
        this.Name = name;
    }
    protected StorageItem(eStags type)
    {
        this.mType = type;
    }
    protected StorageItem(string name, eStags type)
    {
        this.Name = name;
        this.mType = type;
    }
    #endregion

    public virtual string asString() { return ""; }
    public virtual int asInt() { return 0; }
    public virtual uint asUInt() { return 0U; }
    public virtual long asInt64() { return 0; }
    public virtual ulong asUInt64() { return 0U; }
    public virtual double asDouble() { return 0.0; }
    public virtual float asFloat() { return 0.0f; }
    public virtual bool asBool() { return false; }
    public virtual byte asByte() { return 0; }

    public abstract StorageItem Clone();
    public abstract void Set(StorageItem from);

    public abstract bool Equals(StorageItem other);

    public bool Is(string type) { return (Datatag() == type); }
    public bool Is(eStags type) { return (Type == type); }

    public bool Deserialize(XElement input) { return Deserialize(input, 0); }

    public bool Deserialize(XmlReader xmlReader) { return Deserialize(xmlReader, 0); }

    public virtual bool Deserialize(XElement input, uint options)
    {
        if (input.Name != Datatag())
        {
            Log.e("Wrong data tag while loading storage item: {0}", input.Name);
            return false;
        }
        var nameAttr = input.Attribute("name");
        if (nameAttr != null)
        {
            mName = nameAttr.Value;
        }
        return true;
    }

    public virtual bool Deserialize(XmlReader xmlReader, uint options)
    {
        if (xmlReader.EOF)
        {
            Log.e("Reader is end!");
            return false;
        }
        if (xmlReader.Name != Datatag())
        {
            Log.e("Wrong data tag while loading storage item: {0}", xmlReader.Name);
            return false;
        }
        var name = xmlReader["name"];
        if (name != null)
        {
            mName = name;
        }
        return true;
    }

    public bool Serialize(XContainer outParent) { return Serialize(outParent, 0); }
    public virtual bool Serialize(XContainer outParent, uint options)
    {
        XElement elemOut = new XElement(Datatag());
        outParent.Add(elemOut);
        elemOut.Add(new XAttribute("name", string.IsNullOrEmpty(mName) ? string.Empty : mName));
        return true;
    }

    public static void SerializeValue(XContainer outParent, string val)
    {
        XElement output = outParent.Elements().Last();
        XAttribute valueAttr = new XAttribute("value", val);
        output.Add(valueAttr);
    }

    public static string DeserializeValue(XElement input)
    {
        foreach (XAttribute attr in input.Attributes())
        {
            if (attr.Name == "value")
            {
                return attr.Value;
            }
        }
        return "";
    }

    public static string DeserializeValue(XmlReader reader)
    {
        var value = reader["value"];
        return value ?? string.Empty;
    }
}
