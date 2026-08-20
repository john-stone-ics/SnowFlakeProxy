#define MyAppName "SnowFlakeProxy"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "SnowFlakeProxy"
#define MyAppExeName "ASCOM.SnowFlakeProxy.exe"
#define MyAppId "{f81268a0-8c42-4513-ae35-8a131e8fc40d}"

[Setup]
AppId={{f81268a0-8c42-4513-ae35-8a131e8fc40d}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf32}\ASCOM\SnowFlakeProxy
DefaultGroupName=ASCOM\SnowFlakeProxy
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=admin
ArchitecturesAllowed=x86compatible x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
MinVersion=6.1sp1
OutputDir=..\dist
OutputBaseFilename=SnowFlakeProxy-{#MyAppVersion}-Setup
SetupIconFile=..\src\ASCOM.SnowFlakeProxy\ASCOM.ico
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
InfoBeforeFile=InfoBefore.txt
CloseApplications=yes
CloseApplicationsFilter=ASCOM.SnowFlakeProxy.exe
RestartApplications=no
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\src\ASCOM.SnowFlakeProxy\bin\Release\ASCOM.SnowFlakeProxy.exe"; DestDir: "{app}"; Flags: ignoreversion restartreplace
Source: "..\src\ASCOM.SnowFlakeProxy\bin\Release\ASCOM.SnowFlakeProxy.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Readme"; Filename: "{app}\README.txt"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "/regserver"; StatusMsg: "Registering ASCOM LocalServer..."; Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "/unregserver"; RunOnceId: "SnowFlakeProxyUnreg"; Flags: runhidden waituntilterminated

[Code]
function DotNet472Installed: Boolean;
var
  release: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', release) then
    Result := release >= 461808;
end;

function AscomPlatform7Installed: Boolean;
var
  version: String;
  major_text: String;
  dot_pos: Integer;
  major: Integer;
begin
  Result := False;
  if RegQueryStringValue(HKLM, 'SOFTWARE\ASCOM', 'PlatformVersion', version) then
  begin
    dot_pos := Pos('.', version);
    if dot_pos > 0 then
      major_text := Copy(version, 1, dot_pos - 1)
    else
      major_text := version;
    major := StrToIntDef(major_text, 0);
    Result := major >= 7;
  end;
end;

function WandererDriverInstalled: Boolean;
begin
  Result :=
    RegKeyExists(HKLM, 'SOFTWARE\ASCOM\FilterWheel Drivers\ASCOM.WandererSnowflakeFilterWheel1.FilterWheel') or
    RegKeyExists(HKLM, 'SOFTWARE\ASCOM\FilterWheel Drivers\ASCOM.WandererSnowflakeFilterWheel2.FilterWheel') or
    RegKeyExists(HKLM, 'SOFTWARE\ASCOM\FilterWheel Drivers\ASCOM.WandererSnowflakeFilterWheel3.FilterWheel');
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not DotNet472Installed then
  begin
    MsgBox('.NET Framework 4.7.2 or later is required.', mbError, MB_OK);
    Result := False;
    exit;
  end;
  if not AscomPlatform7Installed then
  begin
    MsgBox('ASCOM Platform 7 or later is required. Install it from https://ascom-standards.org then run this setup again.', mbError, MB_OK);
    Result := False;
    exit;
  end;
  if not WandererDriverInstalled then
  begin
    if MsgBox('No Wanderer Snowflake Filter Wheel driver is registered on this PC. The proxy cannot talk to the wheel without it. Continue anyway?', mbConfirmation, MB_YESNO) = IDNO then
      Result := False;
  end;
end;
