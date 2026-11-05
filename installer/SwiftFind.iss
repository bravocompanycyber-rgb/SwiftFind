
#define MyAppName "SwiftFind"
#define MyAppVersion "3.0.0"
#define MyAppPublisher "Bravo Company Cyber"

[Setup]
AppId={{A7D41E23-8F62-4B95-9C31-72B4E619A301}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=Copyright (C) 2026 Bravo Company Cyber

DefaultDirName={autopf}\SwiftFind
DefaultGroupName=SwiftFind

OutputDir=..\dist
OutputBaseFilename=SwiftFind-3.0.0-Setup

SetupIconFile=..\assets\SwiftFind.ico
UninstallDisplayIcon={app}\SwiftFind.exe

Compression=lzma2
SolidCompression=yes
WizardStyle=modern

PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

Uninstallable=yes
CloseApplications=yes

VersionInfoVersion=3.0.0.0
VersionInfoCompany=Bravo Company Cyber
VersionInfoDescription=SwiftFind Windows Installer
VersionInfoProductName=SwiftFind
VersionInfoProductVersion=3.0.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a Desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\dist\SwiftFind-3.0.0-Portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\SwiftFind"; Filename: "{app}\SwiftFind.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\SwiftFind"; Filename: "{app}\SwiftFind.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\SwiftFind.exe"; Description: "Launch SwiftFind"; Flags: nowait postinstall skipifsilent

[Code]
function IsNet48Installed(): Boolean;
var
  Release: Cardinal;
begin
  Result := False;

  if IsWin64 then
  begin
    if RegQueryDWordValue(HKLM64,
      'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
      'Release', Release) then
      Result := Release >= 528040;
  end
  else
  begin
    if RegQueryDWordValue(HKLM,
      'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
      'Release', Release) then
      Result := Release >= 528040;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := IsNet48Installed();

  if not Result then
  begin
    MsgBox(
      'SwiftFind requires Microsoft .NET Framework 4.8 or later. Please install it before continuing.',
      mbError, MB_OK);
  end;
end;