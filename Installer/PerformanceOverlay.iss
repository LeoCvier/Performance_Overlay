#define MyAppName "PerformanceOverlay"
#define MyAppVersion "1.1.20"
#define MyAppPublisher "LeoCvier"
#define MyAppExeName "PerformanceOverlay.exe"

[Setup]
AppId={{8D9D8F21-4D55-4DB5-8D4B-9E2B4B5B7A31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
VersionInfoVersion={#MyAppVersion}.0
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
UsePreviousAppDir=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
CloseApplications=yes
RestartApplications=no
OutputDir=..\Output
OutputBaseFilename=PerformanceOverlay-Setup-v{#MyAppVersion}-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "..\Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Register-Autostart.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Unregister-Autostart.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\PerformanceOverlay"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\PerformanceOverlay"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Register-Autostart.ps1"""; Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Unregister-Autostart.ps1"""; Flags: runhidden waituntilterminated; RunOnceId: "RemovePerformanceOverlayTask"
