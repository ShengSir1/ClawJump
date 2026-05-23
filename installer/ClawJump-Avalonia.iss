#define MyAppName "Claw Jump"
#define MyAppExeName "ClawJump.exe"
#define MyAppVersion "0.2.3"
#define MyAppPublisher "ShengSir"
#define MyAppURL "https://github.com/ShengSir1/ClawJump.git"

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

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

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

[Code]
function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  if UninstallSilent() then
  begin
    Exit;
  end;

  if MsgBox('是否同时从 Claude Code 的 settings.json 中移除 Claw Jump Hook 配置？', mbConfirmation, MB_YESNO) = IDYES then
  begin
    if not Exec(ExpandConstant('{app}\{#MyAppExeName}'), '--cleanup-claude-hooks', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      MsgBox('无法启动 Hook 配置清理命令，卸载将继续。', mbError, MB_OK);
    end
    else if ResultCode <> 0 then
    begin
      MsgBox('Hook 配置清理失败，请稍后手动检查 Claude Code 的 settings.json。卸载将继续。', mbError, MB_OK);
    end;
  end;
end;
