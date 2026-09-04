// The "quiet" installer: one launch, no choices - default install path, every
// supported format associated, then the app starts. The only unavoidable
// interruption is Windows' own UAC prompt (installing to Program Files
// requires admin - that's an OS gate Inno can't skip). Every wizard page that
// CAN be skipped is (welcome/dir/tasks/ready/finished); [Tasks] is empty so
// the tasks page never appears in the first place; the run-after-install
// step fires unconditionally instead of through the (now-skipped) finished
// page's checkbox.
//
// For the version with a directory picker and a per-format association
// picker, see BananaView.iss.
#define MyAppName "BananaView"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "BananaView"
#define MyAppExeName "BananaView.exe"
#define MyAppIcoName "AppIcon.ico"

[Setup]
AppId={{2E7C9E0E-9B77-4E5E-9B7A-2A1F6C5D8B10}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableWelcomePage=yes
DisableDirPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
OutputDir=output
OutputBaseFilename=BananaView-Setup-Quiet
SetupIconFile=..\Viewer\Resources\AppIcon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Удалить {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
; Same "Default Programs" registration as the normal installer (see
; BananaView.iss for the full explanation) - every supported format is
; associated unconditionally since there's no picker page here to ask.
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#MyAppName}"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "BananaView - photo viewer with SAI2/PSD/Procreate support"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: "{app}\{#MyAppIcoName}"
Root: HKLM; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: "Software\{#MyAppName}\Capabilities"; Flags: uninsdeletevalue

Root: HKLM; Subkey: "Software\Classes\BananaView.Image"; ValueType: string; ValueName: ""; ValueData: "Image (BananaView)"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\BananaView.Image\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppIcoName}"
Root: HKLM; Subkey: "Software\Classes\BananaView.Image\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".png"; ValueData: "BananaView.Image"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpg"; ValueData: "BananaView.Image"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpeg"; ValueData: "BananaView.Image"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jfif"; ValueData: "BananaView.Image"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".bmp"; ValueData: "BananaView.Image"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tif"; ValueData: "BananaView.Image"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tiff"; ValueData: "BananaView.Image"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".gif"; ValueData: "BananaView.Image"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".webp"; ValueData: "BananaView.Image"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ico"; ValueData: "BananaView.Image"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".icns"; ValueData: "BananaView.Image"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".psd"; ValueData: "BananaView.Image"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".procreate"; ValueData: "BananaView.Image"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".sai2"; ValueData: "BananaView.Image"

[Run]
; No postinstall/skipifsilent: the Finished page that would normally host
; this as a checkbox is disabled above, so it's fired unconditionally instead.
Filename: "{app}\{#MyAppExeName}"; Flags: nowait

[Code]
procedure SHChangeNotify(wEventId: Longint; uFlags: UINT; dwItem1: LongWord; dwItem2: LongWord);
external 'SHChangeNotify@shell32.dll stdcall';

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    SHChangeNotify($8000000, 0, 0, 0);
end;
