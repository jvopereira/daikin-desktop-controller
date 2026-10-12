namespace DaikinDesktopController.Device;
public enum StatusReadOutcome { Succeeded, KeyRejected, Unreachable, UnexpectedResponse }
public sealed record StatusRead(StatusReadOutcome Outcome, AirConditionerStatus? Status = null);
