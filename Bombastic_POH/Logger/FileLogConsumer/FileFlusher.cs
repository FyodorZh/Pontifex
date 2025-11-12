using System;
using System.IO;
using System.Text;
using LogConsumers;
using Shared.Utils;

namespace FileLogConsumer
{
    public class FileFlusher : IPeriodicLogic
    {
        private readonly long _maxSize;
        private readonly bool _splitByDate;
        private readonly bool _splitByRun;
        private readonly BytesBuffer _logsBuffer;
        private readonly Action<Exception> _onError;
        private readonly string _currentLogFileName;
        private FileStream _currentLogFileStream;
        private readonly int _logFileBufferSize = 1024 * 8;

        private DateTime _currentLogDate;
        private long _currentLogSize;
        private ILogicDriverCtl _driver;

        public FileFlusher(string currentLogFileName, long maxSize, bool splitByDate, bool splitByRun, BytesBuffer logsBuffer, Action<Exception> onError = null)
        {
            _currentLogFileName = currentLogFileName;
            _maxSize = maxSize;
            _splitByDate = splitByDate;
            _splitByRun = splitByRun;
            _logsBuffer = logsBuffer;
            _onError = onError;
        }

        public bool LogicStarted(ILogicDriverCtl driver)
        {
            try
            {
                _driver = driver;

                var file = new FileInfo(_currentLogFileName);
                if (file.Directory != null)
                {
                    Directory.CreateDirectory(file.Directory.FullName);
                }

                _currentLogDate = DateTime.UtcNow.Date;

                if (file.Exists)
                {
                    var lastExistedDate = file.LastWriteTimeUtc.Date;

                    if (_splitByDate && _currentLogDate != lastExistedDate
                        || _maxSize > 0 && file.Length >= _maxSize
                        || _splitByRun)
                    {
                        MoveFileToOld(_currentLogFileName, lastExistedDate);
                    }
                }

                InitFileStream();

                return true;
            }
            catch (Exception e)
            {
                driver.Log.wtf(e);
                return false;
            }
        }

        public void LogicTick()
        {
            try
            {
                if (_currentLogDate != DateTime.UtcNow.Date && _splitByDate
                    || _maxSize > 0 && _currentLogSize >= _maxSize)
                {
                    // скидываем на диск старый лог файл
                    _currentLogFileStream.Flush();
                    _currentLogFileStream.Dispose();

                    MoveFileToOld(_currentLogFileName, _currentLogDate);

                    InitFileStream();
                }

                MoveBufferToStream();

                _currentLogFileStream.Flush();
            }
            catch (Exception e)
            {
                if (_onError != null)
                {
                    _onError(e);
                }
            }
        }

        private void MoveBufferToStream()
        {
            foreach (var log in _logsBuffer)
            {
                _currentLogFileStream.Write(log.Array, log.Offset, log.Count);
                _currentLogSize += log.Count;
            }
        }

        public void Stop()
        {
            LogicStopped();
            var driver = _driver;
            if (driver != null)
            {
                driver.Stop();
            }
        }

        private void InitFileStream()
        {
            if (!File.Exists(_currentLogFileName))
            {
                File.Create(_currentLogFileName).Dispose();
            }

            _currentLogFileStream = new FileStream(_currentLogFileName, FileMode.Append, FileAccess.Write, FileShare.Read, _logFileBufferSize);
            _currentLogSize = _currentLogFileStream.Length;
            _currentLogDate = DateTime.UtcNow.Date;
        }

        public void LogicStopped()
        {
            try
            {
                if (_currentLogFileStream != null)
                {
                    MoveBufferToStream();
                    _currentLogFileStream.Flush();
                    _currentLogFileStream.Dispose();
                    _currentLogFileStream.Close();
                    _currentLogFileStream = null;
                }
            }
            catch (Exception e)
            {
                if (_onError != null)
                {
                    _onError(e);
                }
            }      
        }

        private void MoveFileToOld(string oldFileName, DateTime oldFileDate)
        {
            File.Move(oldFileName, GetNextFileName(oldFileName, oldFileDate));
        }

        /// <returns>
        /// Возращает уникальное имя файла в формате  fileName.yyyy.MM.dd
        /// Если на данную дату уже есть файл, то добавит порядковый номер в виде fileName.yyyy.MM.dd_0000
        /// </returns>
        private string GetNextFileName(string fileName, DateTime lastDate)
        {
            var newFileName = string.Format("{0}.{1}", fileName, lastDate.ToString("yyyy.MM.dd"));
            if (File.Exists(newFileName))
            {
                var dir = Path.GetDirectoryName(newFileName);
                if (string.IsNullOrEmpty(dir))
                {
                    dir = Directory.GetCurrentDirectory();
                }
                var files = Directory.GetFiles(dir, Path.GetFileName(newFileName) + "_????", SearchOption.TopDirectoryOnly);
                var index = 0;
                foreach (var file in files)
                {
                    var i = file.LastIndexOf('_');
                    if (i < 0) continue;
                    if (int.TryParse(file.Substring(i + 1), out i) && i > index)
                    {
                        index = i;
                    }
                }
                ++index;
                newFileName += "_" + index.ToString("0000");
            }

            return newFileName;
        }
    }
}
