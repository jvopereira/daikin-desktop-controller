using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DaikinDesktopController.App.Configuration;
using DaikinDesktopController.App.Localization;
using DaikinDesktopController.App.Panel;
using DaikinDesktopController.App.Settings;
using DaikinDesktopController.App.Startup;
using DaikinDesktopController.App.Tray;
using Forms = System.Windows.Forms;
namespace DaikinDesktopController.App;
public sealed class AppShell(Application application) : IDisposable
{
    private readonly SingleInstance instance = new();
    private readonly TrayClickGate clickGate = new(TimeProvider.System);
    private readonly IAirConditionerConfigurationStore configuration = new NotConfiguredStore();
    private TrayIcon? tray;
    private CompactControlPanel? panel;
    private SettingsWindow? settings;
    public void Start()
    {
        if (!instance.IsFirstInstance) { application.Shutdown(); return; }
        Localizer.Instance.SetCulture(Localizer.SelectCulture(CultureInfo.InstalledUICulture));
        try { new LaunchAtSignIn(new WindowsRunKey()).EnsureEnabledByDefault(System.IO.Path.ChangeExtension(typeof(App).Assembly.Location, ".exe")); }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            MessageBox.Show(Localizer.Instance["StartupRegistrationError"], Localizer.Instance["AppName"]);
        }
        tray = new TrayIcon();
        tray.LeftClicked += (_, _) => TogglePanel();
        tray.ExitRequested += (_, _) => { tray.Dispose(); tray = null; application.Shutdown(); };
        if (StartupDecision.ShouldOpenPanel(configuration)) ShowPanel();
    }
    private void TogglePanel()
    {
        if (panel?.IsVisible == true) { panel.Hide(); return; }
        if (!clickGate.ShouldSuppressClick()) ShowPanel();
    }
    private void ShowPanel()
    {
        if (panel is null)
        {
            panel = new CompactControlPanel();
            panel.SettingsRequested += (_, _) => OpenSettings();
            panel.Deactivated += (_, _) => { if (panel.IsVisible) { clickGate.RecordDismissal(); panel.Hide(); } };
        }
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        var work = screen.WorkingArea;
        var bounds = screen.Bounds;
        var position = PanelPlacement.Compute(new Rect(work.X, work.Y, work.Width, work.Height),
            new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height), new Size(panel.Width, panel.Height));
        // Move the hidden HWND to the target monitor before reading its per-monitor DPI.
        var handle = new WindowInteropHelper(panel).EnsureHandle();
        SetWindowPos(handle, IntPtr.Zero, (int)position.X, (int)position.Y, 0, 0, 0x0015);
        var dpi = VisualTreeHelper.GetDpi(panel);
        position = PanelPlacement.Compute(new Rect(work.X, work.Y, work.Width, work.Height),
            new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            new Size(panel.Width * dpi.DpiScaleX, panel.Height * dpi.DpiScaleY));
        panel.Left = position.X / dpi.DpiScaleX;
        panel.Top = position.Y / dpi.DpiScaleY;
        panel.Show();
        panel.Activate();
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    public void OpenSettings(SettingsSection section = SettingsSection.Secrets)
    {
        panel?.Hide();
        if (settings is null)
        {
            settings = new SettingsWindow();
            settings.Closed += (_, _) => settings = null;
        }
        settings.Show(section);
    }
    public void Dispose() { tray?.Dispose(); instance.Dispose(); }
}
