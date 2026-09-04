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
#define MyAppVersion "1.1.3"
#define MyAppPublisher "BananaView"
#define MyAppExeName "BananaView.exe"
#define MyAppIcoName "AppIcon.ico"
#define MyAppThumbHostName "BananaView.ThumbnailProvider.comhost.dll"

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
; The whole point of this installer is zero prompts - Inno shows a language
; picker before anything else runs whenever more than one [Languages] entry
; is defined, regardless of the Disable* flags above (those only affect
; wizard pages, not this pre-wizard dialog). Suppressing it explicitly rather
; than dropping down to a single language keeps English available as a
; fallback for anyone who'd rather read it, just without ever asking.
ShowLanguageDialog=no
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

; Explorer thumbnail handler (see BananaView.iss for the full explanation of
; IThumbnailProvider/ShellEx) for the four formats Windows has no built-in
; decoder for. {{e357fccd-...} is IThumbnailProvider's fixed IID (also the
; ShellEx subkey name); {{327b8523-...} is our handler class's CLSID. The
; doubled opening brace is Inno's own escape for a literal "{" in a
; declarative value - without it "{e357fccd-...}" would be parsed as an
; (unknown) constant reference, same reason [Setup]'s AppId is written that way.
Root: HKLM; Subkey: "Software\Classes\.psd\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{327b8523-1a5d-4c8d-9d60-611a8acf1572}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\.procreate\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{327b8523-1a5d-4c8d-9d60-611a8acf1572}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\.sai2\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{327b8523-1a5d-4c8d-9d60-611a8acf1572}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\.icns\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{327b8523-1a5d-4c8d-9d60-611a8acf1572}"; Flags: uninsdeletekey

[Run]
; No postinstall/skipifsilent: the Finished page that would normally host
; this as a checkbox is disabled above, so it's fired unconditionally instead.
Filename: "{sys}\regsvr32.exe"; Parameters: "/s ""{app}\{#MyAppThumbHostName}"""; Flags: runhidden
Filename: "{app}\{#MyAppExeName}"; Flags: nowait

[UninstallRun]
Filename: "{sys}\regsvr32.exe"; Parameters: "/u /s ""{app}\{#MyAppThumbHostName}"""; Flags: runhidden; RunOnceId: "UnregisterThumbHandler"

[Code]
procedure SHChangeNotify(wEventId: Longint; uFlags: UINT; dwItem1: LongWord; dwItem2: LongWord);
external 'SHChangeNotify@shell32.dll stdcall';

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    SHChangeNotify($8000000, 0, 0, 0);
end;
