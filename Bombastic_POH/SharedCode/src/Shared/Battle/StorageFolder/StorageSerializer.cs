using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

// Интерфейс для десериализуемых классов
public interface ISerializable
{
    void Deserialize(StorageFolder from);
    void Serialize(StorageFolder to);
}

public interface IUid
{
    UInt32 getUid();
}

/// <summary>
/// Статический класс для загрузки списков данных
/// </summary>
/// <typeparam name="T">Тип загружаемых данных</typeparam>
public static class InfoListLoader<T> where T : ISerializable, new()
{
    public static List<T> Load(StorageFolder from)
    {
        if (from == null || from.Count == 0)
        {
            return null;
        }

        List<T> infoList = new List<T>();
        foreach (StorageItem item in from.Items)
        {
            StorageFolder itemFld = item as StorageFolder;
            if (itemFld == null)
            {
                continue;
            }
            T info = new T();
            info.Deserialize(itemFld);
            infoList.Add(info);
        }
        return infoList;
    }
    public static StorageFolder Save(List<T> list, String folderName, String itemFolderName, StorageFolder to)
    {
        if (to == null || list == null || list.Count == 0)
        {
            return null;
        }
        StorageFolder listFld = new StorageFolder(folderName);
        to.AddItem(listFld);

        foreach (T item in list)
        {
            if (item == null)
            {
                continue;
            }
            StorageFolder fld = new StorageFolder(itemFolderName);
            listFld.AddItem(fld);

            item.Serialize(fld);
        }
        return listFld;
    }
}

/// <summary>
/// Статический класс для загрузки индексированых контейнеров данных
/// </summary>
/// <typeparam name="T">Тип загружаемых данных</typeparam>
public static class InfoDictionaryLoader<T> where T : ISerializable, IUid, new()
{
    public static Dictionary<UInt32, T> Load(StorageFolder from)
    {
        if (from == null || from.Count == 0)
        {
            return null;
        }

        Dictionary<UInt32, T> infoDict = new Dictionary<UInt32, T>();
        foreach (StorageItem item in from.Items)
        {
            StorageFolder itemFld = item as StorageFolder;
            if (itemFld == null)
            {
                continue;
            }
            T info = new T();
            info.Deserialize(itemFld);
            infoDict.Add(info.getUid(), info);
        }
        return infoDict;
    }
    public static StorageFolder Save(Dictionary<UInt32, T> dict, String folderName, String itemFolderName, StorageFolder to)
    {
        if (to == null || dict == null || dict.Count == 0)
        {
            return null;
        }
        StorageFolder dictFld = new StorageFolder(folderName);
        to.AddItem(dictFld);

        foreach (KeyValuePair<UInt32, T> pair in dict)
        {
            if (pair.Value == null)
            {
                continue;
            }
            StorageFolder fld = new StorageFolder(itemFolderName);
            dictFld.AddItem(fld);

            pair.Value.Serialize(fld);
        }
        return dictFld;
    }
}

/// <summary>
/// Класс содержит методы для загрузки и сохранения хранилища Storage Folder в файлы xml
/// </summary>
public static class CStorageSerializer
{
    public static Boolean loadFromXmlDocument(XDocument doc, StorageFolder strg_folder)
    {
        if (doc == null)
        {
            Log.e("XML document is null");
            return false;
        }

        XElement node = doc.Root;

        if (!strg_folder.Deserialize(node))
        {
            Log.e("Error while loadind quest storage");
            return false;
        }

        return true;
    }

    public static bool loadFromXmlDocument(XmlReader xmlReader, StorageFolder storageFolder)
    {
        if (xmlReader == null)
        {
            Log.e("XML document is null");
            return false;
        }

        xmlReader.MoveToContent();

        if (!storageFolder.Deserialize(xmlReader))
        {
            Log.e("Error while loading quest storage");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Загружает данные из указанного xml-файла в хранилище Storage Folder
    /// </summary>
    /// <param name="file">Имя загружаемого файла</param>
    /// <param name="strg_folder">Хранилище, в которое загружается содержимое файла</param>
    /// <param name="createRootFolder">Признак создания корневой папки в указанном strg_folder и загрузка в эту папку содержимого файла</param>
    /// <param name="rootFilename">Имя папки хранилища, в которое загружен файл, устанавливается как имя файла без расширения</param>
    /// <returns></returns>
    public static Boolean loadFromFile(String file, StorageFolder strg_folder, Boolean createRootFolder, Boolean rootFilename)
    {
        return loadFromXmlReader(file, strg_folder, createRootFolder, rootFilename);
    }

    public static Boolean loadFromXDocument(String file, StorageFolder strg_folder, Boolean createRootFolder, Boolean rootFilename)
    {
        XDocument doc = null;
        FileStream fileStream = null;
        try
        {
            fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
            doc = XDocument.Load(XmlReader.Create(fileStream));
        }
        catch (Exception ex)
        {
            Log.e("Failed to load file \"{0}\", see exception details below", file);
            Log.wtf("Loading failed", ex);
            return false;
        }
        finally
        {
            if (fileStream != null)
                fileStream.Close();
        }

        if (createRootFolder)
        {
            StorageFolder tmp = new StorageFolder();
            strg_folder.AddItem(tmp);
            strg_folder = tmp;
        }

        if (!loadFromXmlDocument(doc, strg_folder))
        {
            return false;
        }

        if (rootFilename)
        {
            file = System.IO.Path.GetFileName(file);
            Int32 iPos = file.IndexOf(".");
            String name = file.Substring(0, iPos);
            strg_folder.Name = name;
        }
        return true;
    }

    public static bool loadFromXmlReader(string file, StorageFolder storageFolder, bool createRootFolder, bool rootFilename)
    {
        FileStream fileStream = null;
        XmlReader xmlReader = null;
        try
        {
            fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
            xmlReader = XmlReader.Create(fileStream);
        }
        catch (Exception ex)
        {
            Log.e("Failed to load file \"{0}\", see exception details below", file);
            Log.wtf("Loading failed", ex);

            if (fileStream != null)
                fileStream.Close();
            return false;
        }

        if (createRootFolder)
        {
            var tmp = new StorageFolder();
            storageFolder.AddItem(tmp);
            storageFolder = tmp;
        }

        if (!loadFromXmlDocument(xmlReader, storageFolder))
        {
            fileStream.Close();
            return false;
        }

        if (rootFilename)
        {
            file = Path.GetFileName(file);
            var iPos = file.IndexOf(".");
            var name = file.Substring(0, iPos);
            storageFolder.Name = name;
        }

        fileStream.Close();
        return true;
    }


    /// <summary>
    /// Загружает данные из XDocument в хранилище Storage Folder
    /// </summary>
    /// <param name="doc">используемый XDocument файла</param>
    /// <param name="strg_folder">Хранилище, в которое загружается содержимое файла</param>
    /// <param name="createRootFolder">Признак создания корневой папки в указанном strg_folder и загрузка в эту папку содержимого файла</param>
    /// <returns></returns>
    public static Boolean loadFromXDoc(XDocument doc, StorageFolder strg_folder, Boolean createRootFolder)
    {
        if (createRootFolder)
        {
            StorageFolder tmp = new StorageFolder();
            strg_folder.AddItem(tmp);
            strg_folder = tmp;
        }

        if (!loadFromXmlDocument(doc, strg_folder))
        {
            return false;
        }
    
        return true;
    }


    /// <summary>
    /// Загрузка всех файлов из указанной папки в хранилище Storage Folder
    /// </summary>
    /// <param name="_dataPath">Имя загружаемой папки</param>
    /// <param name="strg_folder">Хранилище, в которое загружается содержимое файлов указанной папки</param>
    /// <param name="group">Признак группировки данных файлов папок в именованое хранилие. Имя хранилища устанавливается как имя папки</param>
    /// <param name="rootFilename">Признак создания корневой папки в хранилищах загружаемых файлов. Имя хранилища равно имени файла без расширения </param>
    /// <param name="xmlExtentionOnly">Признак загрузки только файлов с расширением ".xml" </param>
    /// <returns></returns>
    public static Boolean loadFromDirectory(String _dataPath, StorageFolder strg_folder, Boolean group, Boolean rootFilename, Boolean xmlExtentionOnly)
    {
        StorageFolder tmp = null;
        try
        {
            if (!Directory.Exists(_dataPath))
            {
                return false;
            }

            foreach (String file in StorageTools.GetFiles(_dataPath))
            {
                if (group && tmp == null)
                {
                    tmp = new StorageFolder();
                    // Выделяем имя каталога, в котором находятся файлы
                    int iPos = _dataPath.LastIndexOf("/");
                    tmp.Name = _dataPath.Substring(iPos + 1);

                    strg_folder.AddItem(tmp);
                    strg_folder = tmp;
                }
                if (xmlExtentionOnly && !file.EndsWith(".xml"))
                {
                    continue;
                }
                if (!loadFromFile(file, strg_folder, true, rootFilename))
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Log.e("Load from directory failed {0}, {1}", _dataPath, ex.Message);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Рекурсивная загрузка всех файлов в указаной и вложеных папках в  хранилище Storage Folder
    /// </summary>
    /// <param name="_dataPath">Имя загружаемой папки</param>
    /// <param name="strg_folder">Хранилище, в которое загружается содержимое файлов указанной папки а также всех вложеных папок</param>
    /// <param name="groupByFolder">Признак группировки данных файлов вложеных папок в именованые хранилища. Имя хранилища устанавливается как имя вложеной папки</param>
    /// <param name="rootFilename">Признак создания корневой папки в хранилищах загружаемых файлов. Имя хранилища равно имени файла без расширения</param>
    /// <param name="xmlExtentionOnly">Признак загрузки только файлов с расширением ".xml" </param>
    /// <returns></returns>
    public static Boolean loadDirectoriesRecursive(String _dataPath, StorageFolder strg_folder, Boolean groupByFolder, Boolean rootFilename, Boolean xmlExtentionOnly)
    {
        // Загружаем данные из текущей папки
        if (!loadFromDirectory(_dataPath, strg_folder, groupByFolder, rootFilename, xmlExtentionOnly))
        {
            return false;
        }
        // А теперь из всех остальных папок
        foreach (String dir in StorageTools.GetAllDirectories(_dataPath))
        {
            if (!loadFromDirectory(dir, strg_folder, groupByFolder, rootFilename, xmlExtentionOnly))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Сохранение хранилища данных в xml-файл
    /// </summary>
    /// <param name="filename">Имя файла</param>
    /// <param name="folder">Сохраняемый Storage Folder</param>
    public static void saveStorageToFile(String filename, StorageFolder folder)
    {
        XDocument docOut = saveStorageToXmlDocument(folder);

        docOut.Root.Save(filename);
    }

    public static XDocument saveStorageToXmlDocument(StorageFolder folder)
    {
        XDocument docOut = new XDocument();
        folder.Serialize(docOut);

        return docOut;
    }

    /// <summary>
    /// Сохранение хранилища данных в строку
    /// </summary>
    /// <param name="folder">Сохраняемый Storage Folder</param>
    /// <returns>Строковое представление</returns>
    public static String saveStorageToString(StorageFolder folder)
    {
        return saveStorageToXmlDocument(folder).ToString();
    }
}