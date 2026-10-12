namespace DaikinDesktopController.App.Preferences;
public sealed class PreferencesSession(IPreferencesStore store)
{
    public AppPreferences Current { get; private set; } = store.Load();
    public event Action<AppPreferences, AppPreferences>? Changed;
    public void Update(Func<AppPreferences, AppPreferences> update)
    {
        var old = Current;
        var now = update(old);
        if (old == now) return;
        store.Save(now);
        Current = now;
        Changed?.Invoke(old, now);
    }
}
