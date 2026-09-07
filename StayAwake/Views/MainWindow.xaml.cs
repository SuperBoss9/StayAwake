using System.ComponentModel;
using System.Windows;
using StayAwake.Models;
using StayAwake.Services;
using StayAwake.ViewModels;

namespace StayAwake.Views;

public partial class MainWindow : Window
{
    private readonly AppController _controller;
    private readonly Func<SettingsWindow> _settingsFactory;
    private bool _forceClose;

    public MainWindow(MainViewModel vm, AppController controller, Func<SettingsWindow> settingsFactory)
    {
        InitializeComponent();
        DataContext = vm;
        _controller = controller;
        _settingsFactory = settingsFactory;
        vm.FocusUntilRequested += FocusUntilSection;
        vm.ProcessPickRequested += OpenProcessPicker;

        var s = controller.Settings;
        if (s.WindowLeft.HasValue && s.WindowTop.HasValue)
        {
            Left = s.WindowLeft.Value;
            Top = s.WindowTop.Value;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
        Width = s.WindowWidth > 0 ? s.WindowWidth : Width;
        Height = s.WindowHeight > 0 ? s.WindowHeight : Height;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var win = _settingsFactory();
        win.Owner = this;
        win.ShowDialog();
    }

    public void FocusUntilSection()
    {
        ShowFromTray();
        UntilSection.BringIntoView();
        UntilSection.Focus();
    }

    public void OpenProcessPicker()
    {
        ShowFromTray();
        var picker = new ProcessPickerWindow(new ProcessPickerViewModel()) { Owner = this };
        if (picker.ShowDialog() == true && picker.ViewModel.Result is ProcessRuleModel rule
            && DataContext is MainViewModel vm)
        {
            vm.AddProcessRule(rule);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_forceClose && _controller.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _controller.UpdateSettings(s =>
        {
            s.WindowLeft = Left;
            s.WindowTop = Top;
            s.WindowWidth = Width;
            s.WindowHeight = Height;
        }, evaluate: false);

        if (DataContext is MainViewModel vm)
            vm.Detach();

        base.OnClosing(e);
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
