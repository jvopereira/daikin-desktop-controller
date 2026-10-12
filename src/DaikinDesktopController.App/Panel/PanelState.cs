using System.Windows;
using DaikinDesktopController.Device;
namespace DaikinDesktopController.App.Panel;
public static class PanelState
{
    public static readonly DependencyProperty IsPendingProperty =
        DependencyProperty.RegisterAttached("IsPending", typeof(bool), typeof(PanelState), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.RegisterAttached("Label", typeof(string), typeof(PanelState), new FrameworkPropertyMetadata(""));
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.RegisterAttached("Value", typeof(string), typeof(PanelState), new FrameworkPropertyMetadata(""));
    public static bool GetIsPending(DependencyObject target) => (bool)target.GetValue(IsPendingProperty);
    public static void SetIsPending(DependencyObject target, bool value) => target.SetValue(IsPendingProperty, value);
    public static string GetLabel(DependencyObject target) => (string)target.GetValue(LabelProperty);
    public static void SetLabel(DependencyObject target, string value) => target.SetValue(LabelProperty, value);
    public static string GetValue(DependencyObject target) => (string)target.GetValue(ValueProperty);
    public static void SetValue(DependencyObject target, string value) => target.SetValue(ValueProperty, value);
}
public enum FooterSeverity { Normal, Pending, Error }
public sealed record FooterStatus(string Key, FooterSeverity Severity);
public static class PanelPresentation
{
    public static string ModeIconKey(OperatingMode? mode) => mode switch
    {
        OperatingMode.Cool => "Icon.Snowflake",
        OperatingMode.Heat => "Icon.Sun",
        OperatingMode.Dry => "Icon.Droplet",
        OperatingMode.Fan => "Icon.Fan",
        _ => "Icon.AutoMode"
    };
    // A command that was not applied matters more than an unknown mode, which matters more than pending intent.
    public static FooterStatus Footer(bool anyNotApplied, bool modeUnknown, bool anyPending) =>
        anyNotApplied ? new("CommandNotAppliedMessage", FooterSeverity.Error)
        : modeUnknown ? new("OperatingModeUnknownMessage", FooterSeverity.Error)
        : anyPending ? new("Updating", FooterSeverity.Pending)
        : new("Connected", FooterSeverity.Normal);
}
