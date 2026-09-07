using System.IO;
using System.Text;

namespace StayAwake.Services;

public sealed class LoggingService : IDisposable
{
    private readonly object _sync = new();
    private readonly string _logDirectory;
    private readonly string _logFilePath;
    private readonly long _maxBytes;
    private readonly int _maxFiles;
    private StreamWriter? _writer;
    private bool _disposed;

    public LoggingService(string? logDirectory = null, long maxBytes = 2 * 1024 * 1024, int maxFiles = 5)
    {
        _logDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StayAwake", "Logs");
        Directory.CreateDirectory(_logDirectory);
        _logFilePath = Path.Combine(_logDirectory, "StayAwake.log");
        _maxBytes = maxBytes;
        _maxFiles = maxFiles;
        OpenWriter();
    }

    public string LogDirectory => _logDirectory;

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex == null ? message : $"{message} | {ex}");

    public void Write(string level, string message)
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            try
            {
                RotateIfNeeded();
                _writer ??= OpenWriter();
                _writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}");
                _writer.Flush();
            }
            catch
            {
                // never throw from logger
            }
        }
    }

    private StreamWriter OpenWriter()
    {
        var stream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        return _writer;
    }

    private void RotateIfNeeded()
    {
        try
        {
            var info = new FileInfo(_logFilePath);
            if (!info.Exists || info.Length < _maxBytes)
                return;

            _writer?.Dispose();
            _writer = null;

            for (int i = _maxFiles - 1; i >= 1; i--)
            {
                var src = Path.Combine(_logDirectory, $"stayawake.{i}.log");
                var dst = Path.Combine(_logDirectory, $"stayawake.{i + 1}.log");
                if (File.Exists(dst))
                    File.Delete(dst);
                if (File.Exists(src))
                    File.Move(src, dst);
            }

            var first = Path.Combine(_logDirectory, "stayawake.1.log");
            if (File.Exists(first))
                File.Delete(first);
            if (File.Exists(_logFilePath))
                File.Move(_logFilePath, first);

            // drop oldest beyond max
            var oldest = Path.Combine(_logDirectory, $"stayawake.{_maxFiles + 1}.log");
            if (File.Exists(oldest))
                File.Delete(oldest);

            OpenWriter();
        }
        catch
        {
            try { OpenWriter(); } catch { /* ignore */ }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }
}
