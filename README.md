# Daikin Desktop Controller

Tray-first Windows shell for local Air Conditioner control. First-Run Setup discovers and pairs a local Air Conditioner. Configured launches start in the tray; controls arrive in issue #4.

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
