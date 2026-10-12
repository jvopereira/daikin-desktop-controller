#define MyAppName "Daikin Desktop Controller"
#define MyAppPublisher "jvopereira"
#define MyAppExeName "DaikinDesktopController.App.exe"

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{3BDBA0ED-78DE-4A2D-B605-336972DC39FB}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=DaikinDesktopController-Setup-{#AppVersion}
SetupIconFile=..\src\DaikinDesktopController.App\Assets\TrayIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=no
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "DaikinDesktopController"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
