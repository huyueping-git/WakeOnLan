// AI-code-start lines:126 tool:cursor ai生成
using System;
using System.IO;
using System.Text;

namespace WakeOnLanClient.Services
{
    /// <summary>
    /// 将日志追加到 exe 同级 log 目录下的按日文本文件。
    /// 长时间运行时保持文件句柄，避免每行都开关文件。
    /// </summary>
    public static class FileLogWriter
    {
        private const int KeepDays = 30;
        private static readonly object SyncRoot = new object();
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private static StreamWriter _writer;
        private static string _currentDate;
        private static bool _cleanedOldFiles;

        public static void Write(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            try
            {
                lock (SyncRoot)
                {
                    EnsureWriter();
                    _writer.WriteLine(line);
                    _writer.Flush();
                }
            }
            catch
            {
                CloseWriter();
            }
        }

        public static void Close()
        {
            lock (SyncRoot)
            {
                CloseWriter();
            }
        }

        private static void EnsureWriter()
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            if (_writer != null && string.Equals(_currentDate, today, StringComparison.Ordinal))
            {
                return;
            }

            CloseWriter();

            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
            Directory.CreateDirectory(directory);
            if (!_cleanedOldFiles)
            {
                CleanupOldFiles(directory);
                _cleanedOldFiles = true;
            }

            var filePath = Path.Combine(directory, today + ".txt");
            _writer = new StreamWriter(filePath, true, Utf8)
            {
                AutoFlush = false
            };
            _currentDate = today;
        }

        private static void CloseWriter()
        {
            if (_writer == null)
            {
                return;
            }

            try
            {
                _writer.Flush();
                _writer.Dispose();
            }
            catch
            {
                // 关闭失败忽略
            }

            _writer = null;
            _currentDate = null;
        }

        private static void CleanupOldFiles(string directory)
        {
            try
            {
                var expireBefore = DateTime.Now.Date.AddDays(-KeepDays);
                var files = Directory.GetFiles(directory, "*.txt");
                for (var index = 0; index < files.Length; index++)
                {
                    var name = Path.GetFileNameWithoutExtension(files[index]);
                    DateTime fileDate;
                    if (!DateTime.TryParseExact(name, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out fileDate))
                    {
                        continue;
                    }

                    if (fileDate < expireBefore)
                    {
                        File.Delete(files[index]);
                    }
                }
            }
            catch
            {
                // 清理失败不影响写日志
            }
        }
    }
}
// AI-code-end