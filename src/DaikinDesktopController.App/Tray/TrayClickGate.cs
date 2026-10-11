namespace DaikinDesktopController.App.Tray;
public sealed class TrayClickGate(TimeProvider clock)
{
    private long? dismissedAt;
    public void RecordDismissal() => dismissedAt = clock.GetTimestamp();
    public bool ShouldSuppressClick() => dismissedAt is long timestamp &&
        clock.GetElapsedTime(timestamp) < TimeSpan.FromMilliseconds(300);
}
