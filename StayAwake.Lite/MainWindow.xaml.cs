using System.Windows;
using System.Windows.Threading;

namespace StayAwake.Lite;

public partial class MainWindow : Window
{
    private readonly PowerKeepAlive _power = new();
    private readonly DispatcherTimer _uiTimer;
    private readonly DateTime _startedAt;
    private bool _updatingToggle;

    public MainWindow()
    {
        InitializeComponent();

        _startedAt = DateTime.Now;
        StartedAtText.Text = _startedAt.ToString("g");
        ElapsedText.Text = "00:00:00";

        ApplyPower(keepDisplayOn: false);

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += (_, _) => UpdateElapsed();
        _uiTimer.Start();
    }

    private void DisplayToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingToggle)
            return;
        ApplyPower(DisplayToggle.IsChecked == true);
    }

    private void ApplyPower(bool keepDisplayOn)
    {
        _power.Apply(keepDisplayOn);

        _updatingToggle = true;
        try
        {
            DisplayToggle.IsChecked = keepDisplayOn;
        }
        finally
        {
            _updatingToggle = false;
        }

        StatusText.Text = keepDisplayOn
            ? "Keeping PC + display awake"
            : "Keeping PC awake (display may sleep)";
        DisplayHintText.Text = keepDisplayOn
            ? "On — display stays awake"
            : "Off — display may turn off";
    }

    private void UpdateElapsed()
    {
        var elapsed = DateTime.Now - _startedAt;
        ElapsedText.Text = elapsed.ToString(elapsed.TotalHours >= 24 ? @"d\.hh\:mm\:ss" : @"hh\:mm\:ss");
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _uiTimer.Stop();
        _power.Dispose();
    }
}
