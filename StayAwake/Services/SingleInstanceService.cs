using System.IO.Pipes;
using System.IO;
using System.Text;

namespace StayAwake.Services;

public sealed class SingleInstanceService : IDisposable
{
    // Local\ avoids elevation requirements that Global\ can hit for non-admin users.
    public const string MutexName = "Local\\StayAwake_SingleInstance_Mutex_v1";
    public const string PipeName = "StayAwake_IPC_v1";
    public const string EndMarker = "<<<END>>>";

    private readonly LoggingService _log;
    private Mutex? _mutex;
    private CancellationTokenSource? _cts;
    private Task? _pipeTask;
    private bool _owned;
    private bool _disposed;

    /// <summary>Handler receives command line and returns reply text (may be multi-line).</summary>
    public Func<string, string>? CommandHandler { get; set; }

    public event Action<string>? CommandReceived;

    public SingleInstanceService(LoggingService log) => _log = log;

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        _owned = createdNew;
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
        }
        return _owned;
    }

    public void StartServer()
    {
        if (!_owned) return;
        _cts = new CancellationTokenSource();
        _pipeTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(ct);
                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                await using var writer = new StreamWriter(server, Encoding.UTF8, bufferSize: 1024, leaveOpen: true) { AutoFlush = true };

                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line))
                {
                    await writer.WriteLineAsync(EndMarker);
                    continue;
                }

                var command = line.Trim();
                _log.Info($"IPC received: {command}");
                CommandReceived?.Invoke(command);

                string reply = "OK";
                try
                {
                    if (CommandHandler != null)
                        reply = CommandHandler(command) ?? "OK";
                }
                catch (Exception ex)
                {
                    reply = "ERROR: " + ex.Message;
                    _log.Warn($"IPC handler error: {ex.Message}");
                }

                foreach (var part in reply.Replace("\r\n", "\n").Split('\n'))
                    await writer.WriteLineAsync(part);
                await writer.WriteLineAsync(EndMarker);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Warn($"IPC listen error: {ex.Message}");
                try { await Task.Delay(500, ct); } catch { break; }
            }
        }
    }

    public static async Task<string?> SendCommandAsync(string command, int timeoutMs = 3000)
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            await client.ConnectAsync(timeoutMs);
            await using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

            await writer.WriteLineAsync(command);

            var sb = new StringBuilder();
            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line == null || line == EndMarker)
                    break;
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(line);
            }
            return sb.ToString();
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _cts?.Dispose(); } catch { /* ignore */ }
        if (_owned)
        {
            try { _mutex?.ReleaseMutex(); } catch { /* ignore */ }
        }
        _mutex?.Dispose();
    }
}
