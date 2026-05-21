#define MyAppName "Claw Jump"
#define MyAppExeName "ClawJump.exe"
#define MyAppVersion "0.2.0"
#define MyAppPublisher "ShengSir1"
#define MyAppURL "https://github.com/ShengSir1/claw-jump-windows"

[Setup]
AppId={{D9264037-5D59-42CF-9C02-7E1A8D0D2A20}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

DefaultDirName={localappdata}\Programs\ClawJump
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

OutputDir=..\installer-output
OutputBaseFilename=ClawJump-Avalonia-Setup-{#MyAppVersion}

Compression=lzma
SolidCompression=yes
WizardStyle=modern

PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\src\ClawJump.Avalonia\Assets\claw.ico

CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no


[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "..\publish\avalonia-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Claw Jump"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Claw Jump"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ClawJump"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 Claw Jump"; Flags: nowait postinstall skipifsilent