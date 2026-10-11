namespace DaikinDesktopController.App.Startup;
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex = new(false, @"Local\DaikinDesktopController");
    public bool IsFirstInstance { get; }
    public SingleInstance()
    {
        try { IsFirstInstance = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsFirstInstance = true; }
    }
    public void Dispose()
    {
        if (IsFirstInstance) mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
