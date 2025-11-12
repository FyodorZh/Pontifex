using System;
using System.Xml.Linq;

// Вспомогательные объекты, используются для чтения из StorageItem
// значения некоторого типа.

/// <summary>
/// Базовый класс для чтения значений из StorageItem.
/// </summary>
public abstract class StorageItemReaderBase
{
    /// <summary>
    /// Читает значение из StorageItem. Возвращает false, если некоторым
    /// причинам это не удалось (кроме случаев, когда чтение приводит к 
    /// появлению Exception).
    /// Не пишет сообщений об ошибках на консоль.
    /// В случае, если значение удалось прочитать, дочерние классы, как правило,
    /// сохранят его в поле value.
    /// </summary>
    public Boolean read(StorageItem item) { return read(item, false); }

    /// <summary>
    /// Читает значение из StorageItem. Возвращает false, если некоторым
    /// причинам это не удалось (кроме случаев, когда чтение приводит к 
    /// появлению Exception).
    /// Если check выставлен в true, то пишет сообщения об ошибках на консоль.
    /// В случае, если значение удалось прочитать, дочерние классы, как правило,
    /// сохранят его в поле value.
    /// </summary>        
    public Boolean read(StorageItem item, Boolean check)
    {
        if (null != item)
        {
            String s = item.asString();
            if (!String.IsNullOrEmpty(s))
            {
                // Здесь не знаем тип значения, поэтому зовем абстрактный метод.
                setValue(s);
                return true;
            }
        }
        if (check)
        {
            StorageReaders.warningFailedToRead(this, item);
        }
        return false;
    }

    public abstract void setValue(String s);
}

public class StorageItemReaderUInt32 : StorageItemReaderBase
{
    public override void setValue(String s)
    {
        value = Convert.ToUInt32(s);
    }

    public UInt32 value { get; private set; }
}

public class StorageItemReaderInt32 : StorageItemReaderBase
{
    public override void setValue(String s)
    {
        value = Convert.ToInt32(s);
    }

    public Int32 value { get; private set; }
}

public class StorageItemReaderString : StorageItemReaderBase
{
    public override void setValue(String s)
    {
        value = s;
    }

    public String value { get; private set; }
}

public class StorageReaders
{
    /// <summary>
    /// Объект для чтения значений типа UInt32 из CStorageItem.
    /// </summary>
    public static StorageItemReaderUInt32 asUInt32 = new StorageItemReaderUInt32();

    /// <summary>
    /// Объект для чтения значений типа Int32 из CStorageItem.
    /// </summary>
    public static StorageItemReaderInt32 asInt32 = new StorageItemReaderInt32();

    /// <summary>
    /// Объект для чтения значений типа String из CStorageItem.
    /// </summary>
    public static StorageItemReaderString asString = new StorageItemReaderString();

    public static void warningFailedToRead(StorageItemReaderBase sender, StorageItem item)
    {
        String strSender = sender.GetType().Name;
        String msg = strSender + ": failed to read storage value, ";
        if (null == item)
        {
            msg += "storage item is null.";
        }
        else
        {
            if (!String.IsNullOrEmpty(item.Name))
            {
                msg += "name='" + item.Name + "', ";
            }
            if (!String.IsNullOrEmpty(item.asString()))
            {
                msg += "value='" + item.asString() + "'.";
            }
        }
        Log.w(msg);
    }
}