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
OutputDir=output
OutputBaseFilename=BananaView-Setup
SetupIconFile=..\Viewer\Resources\AppIcon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "associate"; Description: "Зарегистрировать BananaView как доступное приложение для просмотра изображений (появится в 'Приложения по умолчанию' / 'Открыть с помощью')"; GroupDescription: "Ассоциации файлов:"; Flags: checkedonce

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Удалить {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; --- "Default Programs" registration (Capabilities/RegisteredApplications) ---
; This is the standard, Microsoft-documented way for an app to become a
; *selectable* option in Windows' "Default apps" settings and Explorer's
; "Open with" / "Choose another app" dialogs. Windows 10/11 deliberately does
; not let installers silently force a default association (anti-hijacking
; policy) - the user always makes the final pick themselves, either right
; after install (task below opens the picker) or later in Settings.
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#MyAppName}"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "BananaView - photo viewer with SAI2/PSD/Procreate support"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: "{app}\{#MyAppIcoName}"; Tasks: associate
Root: HKLM; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: "Software\{#MyAppName}\Capabilities"; Tasks: associate; Flags: uninsdeletevalue

Root: HKLM; Subkey: "Software\Classes\BananaView.Image"; ValueType: string; ValueName: ""; ValueData: "Image (BananaView)"; Tasks: associate; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\BananaView.Image\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppIcoName}"; Tasks: associate
Root: HKLM; Subkey: "Software\Classes\BananaView.Image\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associate

; One FileAssociations entry per supported extension, all pointing at the
; single BananaView.Image ProgID above.
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".png"; ValueData: "BananaView.Image"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpg"; ValueData: "BananaView.Image"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpeg"; ValueData: "BananaView.Image"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jfif"; ValueData: "BananaView.Image"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".bmp"; ValueData: "BananaView.Image"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tif"; ValueData: "BananaView.Image"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tiff"; ValueData: "BananaView.Image"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".gif"; ValueData: "BananaView.Image"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".webp"; ValueData: "BananaView.Image"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ico"; ValueData: "BananaView.Image"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".icns"; ValueData: "BananaView.Image"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".psd"; ValueData: "BananaView.Image"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".procreate"; ValueData: "BananaView.Image"; Tasks: associate
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".sai2"; ValueData: "BananaView.Image"; Tasks: associate

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
; Jumps the user straight to the Windows "Default apps" picker for BananaView
; so they can actually flip the default in one click if they want to - the
; installer itself is not allowed to do this silently (see comment above).
Filename: "{sys}\control.exe"; Parameters: "/name Microsoft.DefaultPrograms /page pageDefaultProgram\pageAdvancedSettings\ApplicationId\BananaView"; Description: "Открыть настройки приложений по умолчанию, чтобы выбрать BananaView"; Flags: postinstall skipifsilent unchecked; Tasks: associate

[Code]
procedure SHChangeNotify(wEventId: Longint; uFlags: UINT; dwItem1: LongWord; dwItem2: LongWord);
external 'SHChangeNotify@shell32.dll stdcall';

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // SHCNE_ASSOCCHANGED = 0x8000000, SHCNF_IDLIST = 0 - tells Explorer to
    // pick up the new file associations immediately, no logoff/reboot needed.
    SHChangeNotify($8000000, 0, 0, 0);
  end;
end;
