#define MyAppName "PerformanceOverlay"
#define MyAppVersion "1.1.6"
#define MyAppPublisher "LeoCvier"
#define MyAppExeName "PerformanceOverlay.exe"

[Setup]
AppId={{8D9D8F21-4D55-4DB5-8D4B-9E2B4B5B7A31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
VersionInfoVersion={#MyAppVersion}.0
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
UsePreviousAppDir=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
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

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PerformanceOverlay"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue

[Icons]
Name: "{autoprograms}\PerformanceOverlay"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\PerformanceOverlay"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
    Exec(ExpandConstant('{sys}\schtasks.exe'), '/Delete /TN "PerformanceOverlay" /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
