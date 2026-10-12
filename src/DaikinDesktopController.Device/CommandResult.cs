namespace DaikinDesktopController.Device;
public enum CommandOutcome { Confirmed, NotApplied, NoResponse, UnexpectedResponse }
public sealed record CommandResult(CommandOutcome Outcome, AirConditionerStatus? Status = null);
