using DaikinDesktopController.Device;
namespace DaikinDesktopController.App.Monitoring;
public abstract record MonitorState;
public sealed record ReadingStatus : MonitorState;
public sealed record Available(AirConditionerStatus Status) : MonitorState;
public sealed record Unavailable(UnavailableReason Reason) : MonitorState;
public enum UnavailableReason { Unreachable, KeyRejected, ReadError, StorageFailure }
