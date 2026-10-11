# Use .NET WPF for the Windows desktop application

**Status:** Accepted

The MVP will use C#/.NET with WPF for its windows and user interface, and Windows Forms `NotifyIcon` for the system-tray icon and menu. This Windows-focused stack prioritizes direct operating-system integration for the tray-first lifecycle and settings while allowing the compact control panel to use a custom Windows-style design; the Daikin local-protocol implementation remains behind a separate device-control boundary.

## Consequences

- The desktop UI is Windows-specific, which matches the MVP scope.
- The tray integration uses a Windows Forms component alongside the WPF interface.
- The local device-control layer must remain independent of the UI framework so the protocol implementation can be evaluated separately.
