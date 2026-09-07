using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StayAwake.Models;
using StayAwake.Services;

namespace StayAwake.ViewModels;

public partial class ProcessPickerViewModel : ObservableObject
{
    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private ProcessItem? _selected;
    [ObservableProperty] private string _manualName = "";

    public ObservableCollection<ProcessItem> Processes { get; } = new();
    public ObservableCollection<ProcessItem> Filtered { get; } = new();

    public ProcessRuleModel? Result { get; private set; }
    public bool Confirmed { get; private set; }

    public ProcessPickerViewModel()
    {
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        Processes.Clear();
        foreach (var (pid, name) in ProcessMonitorService.GetRunningProcesses())
            Processes.Add(new ProcessItem(pid, name));
        ApplyFilter();
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Filtered.Clear();
        var f = Filter?.Trim() ?? "";
        foreach (var p in Processes)
        {
            if (string.IsNullOrEmpty(f) ||
                p.Name.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                p.Pid.ToString().Contains(f))
            {
                Filtered.Add(p);
            }
        }
    }

    [RelayCommand]
    private void ConfirmSelected()
    {
        if (Selected != null)
        {
            Result = new ProcessRuleModel
            {
                ProcessName = Selected.Name,
                ProcessId = Selected.Pid,
                Enabled = true,
                DisplayName = $"{Selected.Name} ({Selected.Pid})"
            };
            Confirmed = true;
        }
        else if (!string.IsNullOrWhiteSpace(ManualName))
        {
            var name = ManualName.Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];
            Result = new ProcessRuleModel
            {
                ProcessName = name,
                Enabled = true,
                DisplayName = name
            };
            Confirmed = true;
        }
    }
}

public sealed record ProcessItem(int Pid, string Name)
{
    public string Display => $"{Name}  (PID {Pid})";
}
