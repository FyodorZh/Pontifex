using System.Collections.Generic;
using System.IO;

public static class StorageTools
{
    /// <summary>
    /// Возвращает список файлов или каталогов находящихся по заданному пути.
    /// </summary>
    /// <param name="path">Путь для которого нужно возвратать список.</param>
    /// <param name="isGetDirs">
    /// Если true - функция возвращает список каталогов, иначе файлов.
    /// </param>
    /// <returns>Список файлов или каталогов.</returns>
    private static IEnumerable<string> GetInternal(string path, bool isGetDirs)
    {
        if (isGetDirs)
        {
            List<string> dirs = new List<string>(Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly));
            dirs.Sort(System.StringComparer.Ordinal);
            return dirs;
        }
        List<string> files = new List<string>(Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly));
        files.Sort(System.StringComparer.Ordinal);
        return files;
    }

    /// <summary>
    /// Возвращает список файлов для некоторого пути.
    /// </summary>
    /// <param name="path">
    /// Каталог для которого нужно получить список файлов.
    /// </param>
    /// <returns>Список файлов каталога.</returns>
    public static IEnumerable<string> GetFiles(string path)
    {
        return GetInternal(path, false);
    }

    /// <summary>
    /// Возвращает список каталогов для некоторого пути. Функция не перебирает
    /// вложенные подкаталоги!
    /// </summary>
    /// <param name="path">
    /// Каталог для которого нужно получить список подкаталогов.
    /// </param>
    /// <returns>Список файлов каталога.</returns>
    public static IEnumerable<string> GetDirectories(string path)
    {
        return GetInternal(path, true);
    }

    /// <summary>
    /// Функция возвращает список относительных путей ко всем подкаталогам
    /// (в том числе и вложенным) заданного пути.
    /// </summary>
    /// <param name="path">Путь для которого унжно получить подкаталоги.</param>
    /// <returns>Список подкатлогов.</returns>
    public static IEnumerable<string> GetAllDirectories(string path)
    {
        // Сначала перебираем подкаталоги первого уровня вложенности...
        foreach (string subDir in GetDirectories(path))
        {
            // игнорируем имя текущего каталога и родительского.
            if (subDir == ".." || subDir == ".")
                continue;

            // Комбинируем базовый путь и имя подкаталога.
            string relativePath = Path.Combine(path, subDir);

            // возвращам пользователю относительный путь.
            yield return relativePath;

            // Создаем, рекурсивно, итератор для каждого подкаталога и...
            // возвращаем каждый его элемент в качестве элементов текущего итератоа.
            // Этот прием позволяет обойти ограничение итераторов C# 2.0 связанное
            // с нвозможностью вызовов "yield return" из функций вызваемых из 
            // функции итератора. К сожалению это приводит к созданию временного
            // вложенного итератора на каждом шаге рекурсии, но затраты на создание
            // такого объекта относительно не велики, а удобство очень даже ощутимо.
            foreach (string subDir2 in GetAllDirectories(relativePath))
                yield return subDir2;
        }
    }
}