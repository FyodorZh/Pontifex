using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

public class FolderItems : ObjectList<StorageItem>
{
    public FolderItems Clone()
    {
        FolderItems items = new FolderItems();
        int length = Count;
        for (int i = 0; i < length; i++)
        {
            StorageItem arrItem = mItems[i];
            items.AddItem(arrItem.Clone(), false);
        }
        return items;
    }
    public Boolean AddItem(StorageItem item, Boolean checkUniqName)
    {
        if (checkUniqName)
        {
            int length = Count;
            for (int i = 0; i < length; i++)
            {
                StorageItem arrItem = mItems[i];
                if (arrItem.Name == item.Name)
                {
                    Log.e("Dublicate item name");
                    return false;
                }
            }
        }
        mItems.Add(item);
        return true;
    }
    public void SetItem(StorageItem item) { SetItem(item, false); }

    public void SetItem(StorageItem item, Boolean notAdd)
    {
        int length = Count;
        for (int i = 0; i < length; i++)
        {
            StorageItem arrItem = mItems[i];
            if (arrItem.Name == item.Name && arrItem.Type == item.Type)
            {
                mItems[i] = item;
                return;
            }
        }
        if (!notAdd)
        {
            mItems.Add(item);
        }
    }

    public StorageItem GetItem(String name)
    {
#if UNITY_2017_1_OR_NEWER
        return GetItemIndexed(name);
#else
        return GetItemScan(name);
#endif
    }

    private StorageItem GetItemScan(String name)
    {
        for (int i = 0; i < Count; i++)
        {
            StorageItem arrItem = mItems[i];
            if (arrItem.Name == name)
            {
                return arrItem;
            }
        }

        return null;
    }

    private int mPrevGetItemIndex = -1;
    private StorageItem GetItemIndexed(String name)
    {
        int length = Count;
        for (int i = (mPrevGetItemIndex + 1); i < length; i++)
        {
            StorageItem arrItem = mItems[i];
            if (arrItem.Name == name)
            {
                mPrevGetItemIndex = i;
                return arrItem;
            }
        }

        for (int i = 0; i <= mPrevGetItemIndex && i < length; i++)
        {
            StorageItem arrItem = mItems[i];
            if (arrItem.Name == name)
            {
                mPrevGetItemIndex = i;
                return arrItem;
            }
        }

        return null;
    }

    public void RemoveAll()
    {
        mItems.Clear();
    }
    public Boolean RemoveItem(String name, Boolean removeAll)
    {
        Boolean bRemoved = false;
        int length = Count;
        for (int i = 0; i < length; i++)
        {
            StorageItem arrItem = mItems[i];
            if (arrItem.Name == name)
            {
                mItems.RemoveAt(i);
                bRemoved = true;
                if (!removeAll)
                {
                    break;
                }
            }
        }
        return bRemoved;
    }
    public Boolean Equals(FolderItems other)
    {
        int length = Count;
        if (length != other.Count)
        {
            return false;
        }
        for (int i = 0; i < length; i++)
        {
            StorageItem arrItem1 = mItems[i];
            StorageItem arrItem2 = other.mItems[i];
            if (!arrItem1.Equals(arrItem2))
            {
                return false;
            }
        }
        return true;
    }
}

public class StorageFolder : StorageItem
{
    public const String StorageTag = "folder";
    public const String STRG_ITEM_NAME = "strg";

    protected String mValue;
    public String Value
    {
        set { mValue = value; }
        get { return mValue; }
    }

    #region Constructors
    public StorageFolder() : base(eStags.eSTAG_FOLDER) { }
    public StorageFolder(String name) : base(name, eStags.eSTAG_FOLDER) { }
    public StorageFolder(StorageFolder from)
        : base(from.Name, from.Type)
    {
        CopyFrom(from);
    }
    #endregion

    #region Items operations

    private FolderItems mItems = new FolderItems();
    public FolderItems Items { get { return mItems; } }
    public int Count { get { return mItems.Count; } }
    public void Clear() { mItems.Clear(); }

    public void AddItem(StorageItem item) { AddItem(item, false); }
    public void AddItem(StorageItem item, Boolean checkUniqName) { mItems.AddItem(item, checkUniqName); }
    public void SetItem(StorageItem item) { mItems.SetItem(item); }
    public StorageItem GetItem(String name) { return mItems.GetItem(name); }
    public StorageItem GetItem(Int32 index) { return mItems.GetItem(index); }
    public StorageFolder GetFolder(String name) { return (mItems.GetItem(name) as StorageFolder); }
    public StorageItem GetFolderItem(String folderName, String itemName) { return GetFolder(folderName).GetItem(itemName); }

    public void OverwriteFolders(StorageFolder from)
    {
        foreach (StorageItem item in from.Items)
        {
            if (item.Type == eStags.eSTAG_FOLDER)
            {
                StorageFolder to = GetFolder(item.Name);
                if (to != null)
                {
                    to.OverwriteFolders(item as StorageFolder);
                    continue;
                }
            }
            // Склонируем и перезаписываем одноименные folders, отсутствующие - добавляем
            mItems.SetItem(item.Clone());
        }
    }

    public String GetFolderItemAsString(String folderName, String itemName)
    {
        StorageFolder fl = GetFolder(folderName);
        if (fl != null)
        {
            StorageItem fld = fl.GetItem(itemName);
            if (fld != null)
            {
                return fld.asString();
            }
        }
        return null;
    }
    public UInt32 GetFolderItemAsUint(String folderName, String itemName)
    {
        StorageFolder fl = GetFolder(folderName);
        if (fl != null)
        {
            StorageItem fld = fl.GetItem(itemName);
            if (fld != null && fld.asString().Length > 0)
            {
                if (fld.asString().Length > 0)
                {
                    return fld.asUInt();
                }
                else
                    return 0;
            }
            else
            {
                return 0;
            }
        }
        return 0;
    }

    /// <summary>
    /// Вспомогательный метод для чтения значения тип которого определяется первым агрументом.
    /// Возвращает false, если значение не было считано.
    /// </summary>
    public Boolean readFolderItem(StorageItemReaderBase reader, String folderName, String itemName) { return readFolderItem(reader, folderName, itemName, false); }

    /// <summary>
    /// Вспомогательный метод для чтения значения тип которого определяется первым агрументом.
    /// Возвращает false, если значение не было считано.
    /// Если параметр check выставлен в true, то пишет предупреждение на консоль, если значение
    /// не было считано.
    /// </summary>
    public Boolean readFolderItem(StorageItemReaderBase reader, String folderName, String itemName, Boolean check)
    {
        StorageFolder folder = GetFolder(folderName);
        if (null != folder)
        {
            StorageItem folderItem = folder.GetItem(itemName);
            return reader.read(folderItem, check);
        }
        else if (check)
        {
            String msg = "Can't read storage folder";
            if (!String.IsNullOrEmpty(folderName))
            {
                msg += " '" + folderName + "'";
            }
            Log.w(msg);
        }
        return false;
    }

    /// <summary>
    /// Вспомогательный метод для чтения значения тип которого определяется первым агрументом.
    /// Возвращает false, если значение не было считано.
    /// </summary>
    public Boolean readFolderItem(StorageItemReaderBase reader, String folderName, Boolean check) { return readFolderItem(reader, folderName, STRG_ITEM_NAME, check); }

    /// <summary>
    /// Вспомогательный метод для чтения значения тип которого определяется первым агрументом.
    /// Возвращает false, если значение не было считано.
    /// Если параметр check выставлен в true, то пишет предупреждение на консоль, если значение
    /// не было считано.
    /// </summary>
    public Boolean readFolderItem(StorageItemReaderBase reader, String folderName) { return readFolderItem(reader, folderName, STRG_ITEM_NAME, false); }

    public StorageFolder GetFolderMustPresent(String name)
    {
        StorageFolder folder = GetFolder(name);
        if (folder == null)
        {
            Log.e("Folder not found: {0}", name);
        }
        return folder;
    }
    public String GetItemAsString(String name) { return GetItemAsString(name, ""); }
    public String GetItemAsString(String name, String def)
    {
        StorageItem item = GetItem(name);
        if (item != null)
        {
            return item.asString();
        }
        return def;
    }
    public Int32 GetItemAsInt(String name) { return GetItemAsInt(name, 0); }
    public Int32 GetItemAsInt(String name, Int32 def)
    {
        StorageItem item = GetItem(name);
        if (item != null && item.asString().Length > 0)
        {
            return item.asInt();
        }
        return def;
    }
    public UInt32 GetItemAsUInt(String name) { return GetItemAsUInt(name, 0U); }
    public UInt32 GetItemAsUInt(String name, UInt32 def)
    {
        StorageItem item = GetItem(name);
        if (item != null)
        {
            return item.asUInt();
        }
        return def;
    }
    public Int64 GetItemAsInt64(String name) { return GetItemAsInt64(name, 0); }
    public Int64 GetItemAsInt64(String name, Int64 def)
    {
        StorageItem item = GetItem(name);
        if (item != null && item.asString().Length > 0)
        {
            return item.asInt64();
        }
        return def;
    }
    public UInt64 GetItemAsUInt64(String name) { return GetItemAsUInt64(name, 0U); }
    public UInt64 GetItemAsUInt64(String name, UInt64 def)
    {
        StorageItem item = GetItem(name);
        if (item != null)
        {
            return item.asUInt64();
        }
        return def;
    }
    public Double GetItemAsDouble(String name) { return GetItemAsDouble(name, 0.0); }
    public Double GetItemAsDouble(String name, Double def)
    {
        StorageItem item = GetItem(name);
        if (item != null)
        {
            return item.asDouble();
        }
        return def;
    }
    public Single GetItemAsFloat(String name) { return GetItemAsFloat(name, 0.0f); }
    public Single GetItemAsFloat(String name, Single def)
    {
        StorageItem item = GetItem(name);
        if (item != null)
        {
            return item.asFloat();
        }
        return def;
    }

    public object GetItemAsT<T>(string name)
    {
        StorageItem item = GetItem(name);
        if (null != item)
        {
            switch (System.Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.Boolean:
                    {
                        return item.asBool();
                    }
                case TypeCode.Double:
                    {
                        return item.asDouble();
                    }
                case TypeCode.Single:
                    {
                        return item.asFloat();
                    }
                case TypeCode.Int32:
                    {
                        return item.asInt();
                    }
                case TypeCode.Int64:
                    {
                        return item.asInt64();
                    }
                case TypeCode.String:
                    {
                        return item.asString();
                    }
                case TypeCode.UInt32:
                    {
                        return item.asUInt();
                    }
                case TypeCode.UInt64:
                    {
                        return item.asUInt64();
                    }
                default:
                    {
                        Log.e("Invalid type '{0}' for method GetItemAsT<T>", typeof(T));
                        break;
                    }
            }
        }
        return default(T);
    }

    public Boolean GetItemAsBool(String name)
    {
        return GetItemAsBool(name, false);
    }

    public Boolean GetItemAsBool(String name, Boolean def)
    {
        StorageItem item = GetItem(name);
        if (item != null)
        {
            return item.asBool();
        }
        return def;
    }

    public Byte GetItemAsByte(String name)
    {
        return GetItemAsByte(name, 0);
    }

    public Byte GetItemAsByte(String name, Byte def)
    {
        StorageItem item = GetItem(name);
        if (item != null)
        {
            return item.asByte();
        }
        return def;
    }
    public Boolean RemoveItem(StorageItem item) { return mItems.RemoveItem(item); }
    public Boolean RemoveItem(String name) { return mItems.RemoveItem(name, false); }
    public Boolean RemoveAllItems(String name) { return mItems.RemoveItem(name, true); }
    public void RemoveAllItems() { mItems.RemoveAll(); }
    #endregion

    override public String Datatag()
    {
        return StorageTag;
    }
    public void CopyFrom(StorageFolder from)
    {
        mItems = from.mItems.Clone();
    }
    override public StorageItem Clone()
    {
        return new StorageFolder(this);
    }
    override public void Set(StorageItem from)
    {
        Log.e("StorageFolder.Set: unsupported");
    }
    override public Boolean Equals(StorageItem other)
    {
        if (other.Is(mType))
        {
            StorageFolder other_type = (other as StorageFolder);
            if (other_type == null)
            {
                Log.e("Wrong type convertion");
            }

            return mItems.Equals(other_type.mItems);
        }
        return false;
    }

    override public Boolean Deserialize(XElement input, UInt32 options)
    {
        if (!base.Deserialize(input, options))
        {
            return false;
        }
        
        mValue = DeserializeValue(input);

        foreach (XElement node in input.Elements())
        {
            StorageItem item = StorageItem.Build(node.Name.LocalName);
            if (item != null)
            {
                if (item.Deserialize(node))
                {
                    AddItem(item);
                }
                else
                {
                    return false;
                }
            }
        }

        return true;
    }

    public override bool Deserialize(XmlReader reader, uint options)
    {
        if (!base.Deserialize(reader, options))
        {
            return false;
        }

        mValue = DeserializeValue(reader);

        if (!reader.IsEmptyElement)
        {
            while (reader.Read() &&
                   reader.IsStartElement())
            {
                var item = Build(reader.Name);
                if (item != null)
                {
                    if (item.Deserialize(reader))
                    {
                        AddItem(item);
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    override public Boolean Serialize(XContainer outParent, UInt32 options)
    {
        if (!base.Serialize(outParent, options))
        {
            return false;
        }

        if (!String.IsNullOrEmpty(mValue))
        {
            StorageItem.SerializeValue(outParent, mValue);
        }

        XElement output = outParent.Elements().LastOrDefault();

        if (output != null)
        {
            foreach (StorageItem item in mItems)
            {
                item.Serialize(output, options);
            }
        }

        return true;
    }
}

