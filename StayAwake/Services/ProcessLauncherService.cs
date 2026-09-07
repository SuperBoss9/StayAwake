using System.Diagnostics;
using System.IO;

namespace StayAwake.Services;

public sealed class ProcessLauncherService
{
    private readonly LoggingService _log;
    private Process? _launched;
    private CancellationTokenSource? _watchCts;

    public bool IsRunning => _launched is { HasExited: false };
    public int? LaunchedPid => IsRunning ? _launched!.Id : null;
    public string? LaunchedName { get; private set; }

    public event Action? ProcessExited;

    public ProcessLauncherService(LoggingService log) => _log = log;

    public bool Launch(string path, string? workingDirectory = null, string? args = null)
    {
        StopWatch();
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                _log.Warn($"Launch path missing: {path}");
                return false;
            }

            var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
            var extra = string.IsNullOrWhiteSpace(args) ? "" : " " + args.Trim();
            var psi = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? System.IO.Path.GetDirectoryName(path) ?? Environment.CurrentDirectory
                    : workingDirectory
            };

            if (extension == ".ps1")
            {
                psi.FileName = ResolvePwsh() ?? "powershell.exe";
                psi.Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{path}\"{extra}";
            }
            else if (extension is ".bat" or ".cmd")
            {
                psi.FileName = "cmd.exe";
                psi.Arguments = $"/c \"{path}\"{extra}";
            }
            else
            {
                psi.FileName = path;
                psi.Arguments = args?.Trim() ?? "";
            }

            _launched = Process.Start(psi);
            if (_launched == null)
                return false;

            LaunchedName = System.IO.Path.GetFileNameWithoutExtension(path);
            _log.Info($"Launched under awake: {path} args='{args}' PID={_launched.Id}");
            _watchCts = new CancellationTokenSource();
            _ = WatchAsync(_watchCts.Token);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Launch failed", ex);
            return false;
        }
    }

    private static string? ResolvePwsh()
    {
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(dir.Trim('"'), "pwsh.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }

    private async Task WatchAsync(CancellationToken ct)
    {
        try
        {
            if (_launched == null) return;
            await _launched.WaitForExitAsync(ct);
            _log.Info("Launched process exited");
            ProcessExited?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _log.Warn($"Launch watch error: {ex.Message}");
        }
    }

    public void StopWatch()
    {
        try { _watchCts?.Cancel(); } catch { /* ignore */ }
        _watchCts?.Dispose();
        _watchCts = null;
        try { _launched?.Dispose(); } catch { /* ignore */ }
        _launched = null;
        LaunchedName = null;
    }
}
