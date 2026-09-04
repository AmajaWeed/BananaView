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

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Setup-time-only resources for the custom "which formats" picker page below -
; NOT installed into {app}, just extracted to {tmp} while the wizard runs.
Source: "icons\*.bmp"; DestDir: "{tmp}"; Flags: dontcopy

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
;
; Written unconditionally (not tied to which formats get checked on the
; picker page below) - it just makes BananaView *listable*; actual per-format
; FileAssociations entries are what determines what it's offered for, and
; those are written from [Code] based on the picker's checked state.
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#MyAppName}"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "BananaView - photo viewer with SAI2/PSD/Procreate support"
Root: HKLM; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: "{app}\{#MyAppIcoName}"
Root: HKLM; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: "Software\{#MyAppName}\Capabilities"; Flags: uninsdeletevalue

Root: HKLM; Subkey: "Software\Classes\BananaView.Image"; ValueType: string; ValueName: ""; ValueData: "Image (BananaView)"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\BananaView.Image\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppIcoName}"
Root: HKLM; Subkey: "Software\Classes\BananaView.Image\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
; Jumps the user straight to the Windows "Default apps" picker for BananaView
; so they can actually flip the default in one click if they want to - the
; installer itself is not allowed to do this silently (see comment above).
Filename: "{sys}\control.exe"; Parameters: "/name Microsoft.DefaultPrograms /page pageDefaultProgram\pageAdvancedSettings\ApplicationId\BananaView"; Description: "Открыть настройки приложений по умолчанию, чтобы выбрать BananaView"; Flags: postinstall skipifsilent unchecked; Check: AnyFormatChecked

[Code]
procedure SHChangeNotify(wEventId: Longint; uFlags: UINT; dwItem1: LongWord; dwItem2: LongWord);
external 'SHChangeNotify@shell32.dll stdcall';

type
  TFormatEntry = record
    Ext: String;
    Caption: String;
    Icon: String;
    CheckBox: TNewCheckBox;
  end;

var
  FormatsPage: TWizardPage;
  Formats: array of TFormatEntry;

procedure AddFormat(Ext, Caption, Icon: String);
var
  i: Integer;
begin
  i := GetArrayLength(Formats);
  SetArrayLength(Formats, i + 1);
  Formats[i].Ext := Ext;
  Formats[i].Caption := Caption;
  Formats[i].Icon := Icon;
end;

// One row per extension, each with its own small format badge (see
// gen_icons.py) next to a real checkbox - TNewCheckListBox has no supported
// per-item icon API in Pascal Script, so each row is built by hand from a
// TBitmapImage + TNewCheckBox pair instead. All checked by default; the user
// unchecks anything they'd rather keep opening in another app.
procedure InitializeWizard;
var
  IconImg: TBitmapImage;
  i, Col, Row, ColWidth, RowHeight, X, Y: Integer;
begin
  AddFormat('.png', 'PNG', 'png.bmp');
  AddFormat('.jpg', 'JPG', 'jpg.bmp');
  AddFormat('.jpeg', 'JPEG', 'jpg.bmp');
  AddFormat('.jfif', 'JFIF', 'jfif.bmp');
  AddFormat('.bmp', 'BMP', 'bmp.bmp');
  AddFormat('.tif', 'TIF', 'tif.bmp');
  AddFormat('.tiff', 'TIFF', 'tif.bmp');
  AddFormat('.gif', 'GIF (анимация)', 'gif.bmp');
  AddFormat('.webp', 'WEBP', 'webp.bmp');
  AddFormat('.ico', 'ICO', 'ico.bmp');
  AddFormat('.icns', 'ICNS', 'icns.bmp');
  AddFormat('.psd', 'PSD', 'psd.bmp');
  AddFormat('.procreate', 'Procreate', 'procreate.bmp');
  AddFormat('.sai2', 'SAI2', 'sai2.bmp');

  FormatsPage := CreateCustomPage(wpSelectTasks, 'Ассоциации файлов',
    'Выберите, какие форматы изображений будет открывать BananaView по умолчанию');

  ColWidth := FormatsPage.SurfaceWidth div 2;
  RowHeight := ScaleY(28);

  for i := 0 to GetArrayLength(Formats) - 1 do
  begin
    Col := i mod 2;
    Row := i div 2;
    X := Col * ColWidth;
    Y := Row * RowHeight;

    ExtractTemporaryFile(Formats[i].Icon);
    IconImg := TBitmapImage.Create(FormatsPage);
    IconImg.Left := X;
    IconImg.Top := Y;
    IconImg.Width := ScaleX(24);
    IconImg.Height := ScaleY(24);
    IconImg.Bitmap.LoadFromFile(ExpandConstant('{tmp}\') + Formats[i].Icon);
    IconImg.Parent := FormatsPage.Surface;

    Formats[i].CheckBox := TNewCheckBox.Create(FormatsPage);
    Formats[i].CheckBox.Left := X + ScaleX(30);
    Formats[i].CheckBox.Top := Y + ScaleY(4);
    Formats[i].CheckBox.Width := ColWidth - ScaleX(34);
    Formats[i].CheckBox.Height := ScaleY(17);
    Formats[i].CheckBox.Caption := Formats[i].Ext + '  (' + Formats[i].Caption + ')';
    Formats[i].CheckBox.Checked := True;
    Formats[i].CheckBox.Parent := FormatsPage.Surface;
  end;
end;

function AnyFormatChecked: Boolean;
var
  i: Integer;
begin
  Result := False;
  for i := 0 to GetArrayLength(Formats) - 1 do
    if Formats[i].CheckBox.Checked then
    begin
      Result := True;
      Exit;
    end;
end;

procedure RegisterCheckedFormats;
var
  i: Integer;
begin
  for i := 0 to GetArrayLength(Formats) - 1 do
  begin
    if Formats[i].CheckBox.Checked then
      RegWriteStringValue(HKLM, 'Software\{#MyAppName}\Capabilities\FileAssociations', Formats[i].Ext, 'BananaView.Image')
    else
      RegDeleteValue(HKLM, 'Software\{#MyAppName}\Capabilities\FileAssociations', Formats[i].Ext);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    RegisterCheckedFormats;
    // SHCNE_ASSOCCHANGED = 0x8000000, SHCNF_IDLIST = 0 - tells Explorer to
    // pick up the new file associations immediately, no logoff/reboot needed.
    SHChangeNotify($8000000, 0, 0, 0);
  end;
end;
