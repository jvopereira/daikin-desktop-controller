using System.Windows;
namespace DaikinDesktopController.App;
public partial class App : Application
{
    private AppShell? shell;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        shell = new AppShell(this);
        shell.Start();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        shell?.Dispose();
        base.OnExit(e);
    }
}
