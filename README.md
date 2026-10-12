# Daikin Desktop Controller

Tray-first Windows shell for local Air Conditioner control. First-Run Setup discovers and pairs a local Air Conditioner. Configured launches start in the tray. While the Compact Control Panel is visible, it shows current power and temperatures and refreshes at the chosen Status Refresh Interval. Power and target temperature controls show pending requests and reconcile against device status without automatic resend.

## Build and run

Requires Windows 10 or 11 and the .NET 10 SDK.

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet run --project src/DaikinDesktopController.App
```

Sign-in registration points to the application executable beside its assembly, including when launched through `dotnet run`. The application enables Launch at Sign-in on each start under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` with value name `DaikinDesktopController`. Preferences are saved automatically. To remove this registration after uninstalling, delete that value in Registry Editor.

## Windows manual verification

- Launch: one Tray Icon and a borderless Compact Control Panel near the tray.
- Click the desktop: panel hides. Left-click the Tray Icon: panel reopens. Click it while open: panel closes without reopening.
- Right-click the Tray Icon: only Exit appears. Exit removes the icon and ends the process.
- Open Settings: a standard window with Secrets, Device, General in order. Reopen Settings: the same window activates. Closing Settings leaves the app Running in the Tray.
- Press Alt+F4 in the panel: it hides and the app continues running.
- Start a second process: no second Tray Icon appears.
- Check the Run registry entry and sign out/in to verify Launch at Sign-in.
- Test Brazilian Portuguese, English, and an unsupported Windows display language (falls back to Brazilian Portuguese).
- Test each taskbar edge, auto-hide, and multiple monitors with different scaling.

Windows CI builds the WPF application and runs the logic tests. WPF cannot run on macOS.

## Pairing verification on Windows

1. Launch without configuration and open Secrets from First-Run Setup.
2. Enter a valid Device Secret Key, test reveal/hide, and Save. Verify discovery progress, automatic pairing for one unit, and selection for several units.
3. Relaunch and verify tray-only startup and the `DaikinDesktopController/DeviceSecretKey` credential in Windows Credential Manager.
4. Save an incorrect key and verify Key rejected with the Secrets shortcut.
5. Enter a manual IPv4 address in Device settings. Verify direct pairing, discovery fallback for an unreachable address, and Not found when disconnected.
6. Switch between English and Brazilian Portuguese in each setup state and verify live text updates.

Real-device pairing and Credential Manager require Windows and the target LAN.

Device-control tests target plain .NET 10 and can also run on macOS with `dotnet test tests/DaikinDesktopController.Device.Tests`. App tests and UI verification require Windows.

## Status monitoring verification on Windows

1. Open a configured panel from the Tray Icon and verify an immediate status read.
2. Change Status Refresh Interval in Settings → Device to 5, 10, 30, or 60 seconds.
3. Disconnect the Air Conditioner and verify Air Conditioner Unavailable appears after one retry, with saved configuration retained.
4. Reconnect and verify automatic recovery while the panel stays open.
5. Switch App Language and verify decimal temperatures use a comma in Brazilian Portuguese.
6. Hide the panel via desktop click, Settings, or Alt+F4 and verify status reads stop; reopening reads immediately.
7. Verify unavailable states appear in the panel without Windows notifications.

## Power and temperature verification on Windows

Writes require real-device verification per ADR 0004.

1. Toggle power and confirm the unit and panel agree.
2. Click + and − rapidly; each click sends one command and the final value sticks.
3. Drag the slider; one command is sent on release. Test keyboard and track clicks too.
4. Switch to Fan or Dry in the official app; temperature is disabled with an explanation.
5. Disconnect the network mid-command; the app refreshes without resending, then shows Unavailable with controls disabled.
6. Check English and Brazilian Portuguese, pending hints, and rejection errors.

## Turbo, Comfort and Economy verification on Windows

Writes require real-device verification per ADR 0004.

1. Turn each feature on and off; confirm the unit and panel agree, with Pending shown immediately and then cleared after confirmation.
2. Enable Turbo while Economy is on and verify Economy turns off. Enable Economy while Turbo is on and verify Turbo turns off. Comfort remains independent.
3. Test a status with a feature field absent or invalid: its toggle is disabled with a “not reported” explanation while the remaining status is displayed.
4. Disconnect the network mid-command: verify a status refresh without resend, followed by Unavailable when the unit cannot be reached. Reconnect and retry manually.
5. Verify a refused command restores the confirmed value with an error and a manual retry clears the error while pending.
6. Switch App Language to Brazilian Portuguese and verify labels, pending hints, unsupported explanations, and rejection errors update.
