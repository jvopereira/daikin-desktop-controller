# Daikin Desktop Controller

Tray-first Windows shell for local Air Conditioner control. Device setup and controls will be added in later issues. Until then, every launch opens First-Run Setup.

## Build and run

Requires Windows 10 or 11 and the .NET 10 SDK.

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet run --project src/DaikinDesktopController.App
```

Sign-in registration points to the application executable beside its assembly, including when launched through `dotnet run`. The application enables Launch at Sign-in on each start under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` with value name `DaikinDesktopController`. Saved preferences arrive in issue #2. To remove this registration after uninstalling, delete that value in Registry Editor.

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
