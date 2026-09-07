using StayAwake.Models;

namespace StayAwake.Services;

public sealed class CliOptions
{
    public bool ShowHelp { get; set; }
    public bool ShowWindow { get; set; }
    public bool Minimized { get; set; }
    public bool Quit { get; set; }
    public bool Toggle { get; set; }
    public bool Pause { get; set; }
    public bool Resume { get; set; }
    public bool Status { get; set; }
    public AwakeMode? SetMode { get; set; }
    public int? TimedMinutes { get; set; }
    public string? UntilTime { get; set; }
    public bool? KeepDisplay { get; set; }
    public string? ProcessName { get; set; }
    public int? ProcessId { get; set; }
    public string? LaunchPath { get; set; }
    public string? LaunchArgs { get; set; }
    public string RawForward { get; set; } = string.Empty;
}

public static class CommandLineService
{
    public static CliOptions Parse(string[] args)
    {
        var opt = new CliOptions { RawForward = RebuildCommandLine(args) };
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a.ToLowerInvariant())
            {
                case "-h":
                case "--help":
                case "/?":
                    opt.ShowHelp = true;
                    break;
                case "--show":
                    opt.ShowWindow = true;
                    break;
                case "--minimized":
                case "-m":
                    opt.Minimized = true;
                    break;
                case "--quit":
                case "--exit":
                    opt.Quit = true;
                    break;
                case "--toggle":
                    opt.Toggle = true;
                    break;
                case "--pause":
                    opt.Pause = true;
                    break;
                case "--resume":
                    opt.Resume = true;
                    break;
                case "--status":
                    opt.Status = true;
                    break;
                case "--off":
                    opt.SetMode = AwakeMode.Off;
                    break;
                case "--indefinite":
                case "--on":
                    opt.SetMode = AwakeMode.Indefinite;
                    break;
                case "--timed":
                case "--timer":
                    opt.SetMode = AwakeMode.Timed;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int mins))
                    {
                        opt.TimedMinutes = mins;
                        i++;
                    }
                    break;
                case "--until":
                    opt.SetMode = AwakeMode.Until;
                    if (i + 1 < args.Length)
                        opt.UntilTime = args[++i];
                    break;
                case "--rules":
                    opt.SetMode = AwakeMode.Rules;
                    break;
                case "--display":
                case "--display-on":
                    opt.KeepDisplay = true;
                    break;
                case "--no-display":
                case "--display-off":
                    opt.KeepDisplay = false;
                    break;
                case "--process":
                    if (i + 1 < args.Length)
                        opt.ProcessName = args[++i];
                    break;
                case "--pid":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int pid))
                    {
                        opt.ProcessId = pid;
                        i++;
                    }
                    break;
                case "--launch":
                case "--run":
                    if (i + 1 < args.Length)
                        opt.LaunchPath = args[++i];
                    break;
                case "--args":
                    if (i + 1 < args.Length)
                        opt.LaunchArgs = args[++i];
                    break;
            }
        }
        return opt;
    }

    public static string RebuildCommandLine(string[] args) =>
        string.Join(' ', args.Select(QuoteIfNeeded));

    public static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";
        if (value.Contains(' ') || value.Contains('\t') || value.Contains('"'))
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        return value;
    }

    public static string[] SplitArgs(string commandLine)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(commandLine))
            return result.ToArray();

        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < commandLine.Length; i++)
        {
            char c = commandLine[i];
            if (c == '\\' && i + 1 < commandLine.Length && commandLine[i + 1] == '"')
            {
                current.Append('"');
                i++;
                continue;
            }
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0)
            result.Add(current.ToString());
        return result.ToArray();
    }

    public static string HelpText =>
        """
        StayAwake — prevent Windows sleep without input simulation

        Usage:
          StayAwake [options]

        Options:
          --show                 Open main window (second instance)
          --minimized            Start hidden in tray
          --on / --indefinite    Keep awake indefinitely
          --off                  Turn off
          --timer / --timed <m>  Timed awake (minutes)
          --until <HH:mm>        Awake until local time
          --rules                Combined rules mode
          --display-on / --display
          --display-off / --no-display
          --process "name"       Rules mode + process name
          --pid <id>             Rules mode + process PID
          --run / --launch <path>  Launch under awake
          --args "..."           Arguments for --run
          --pause / --resume     Pause or resume
          --toggle               Toggle off/indefinite
          --status               Print status from running instance
          --quit                 Exit running instance
          --help                 Show help
        """;
}
