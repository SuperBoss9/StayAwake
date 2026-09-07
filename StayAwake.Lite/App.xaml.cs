using System.Windows;

namespace StayAwake.Lite;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeService.Initialize();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        ThemeService.Shutdown();
    }
}
