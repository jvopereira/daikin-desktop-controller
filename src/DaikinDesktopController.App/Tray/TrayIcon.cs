using System.ComponentModel;
using System.Drawing;
using System.Windows;
using DaikinDesktopController.App.Localization;
using Forms = System.Windows.Forms;
namespace DaikinDesktopController.App.Tray;
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon notifyIcon;
    private readonly Forms.ContextMenuStrip menu = new();
    private readonly Forms.ToolStripMenuItem exit = new();
    private readonly Icon icon;
    public event EventHandler? LeftClicked;
    public event EventHandler? ExitRequested;
    public TrayIcon()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/TrayIcon.ico"))!.Stream;
        icon = new Icon(stream);
        menu.Items.Add(exit);
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        notifyIcon = new Forms.NotifyIcon { Icon = icon, ContextMenuStrip = menu };
        notifyIcon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) LeftClicked?.Invoke(this, EventArgs.Empty); };
        Localizer.Instance.PropertyChanged += CultureChanged;
        UpdateText();
        notifyIcon.Visible = true;
    }
    private void CultureChanged(object? sender, PropertyChangedEventArgs e) => UpdateText();
    private void UpdateText()
    {
        notifyIcon.Text = Localizer.Instance["AppName"];
        exit.Text = Localizer.Instance["Exit"];
    }
    public void Dispose()
    {
        Localizer.Instance.PropertyChanged -= CultureChanged;
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        menu.Dispose();
        icon.Dispose();
    }
}
