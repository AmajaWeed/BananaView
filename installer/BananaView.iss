#define MyAppName "BananaView"
#define MyAppVersion "1.2.0"
#define MyAppPublisher "BananaView"
#define MyAppExeName "BananaView.exe"
#define MyAppIcoName "AppIcon.ico"
#define MyAppThumbHostName "BananaView.ThumbnailProvider.comhost.dll"
; IID of IThumbnailProvider (fixed by Microsoft, not ours to choose) - this
; exact string is both the interface GUID and the ShellEx subkey name
; Explorer looks under. No braces here deliberately - see the [Code] comment
; above RegisterThumbnailHandler for why they're added at each use site
; instead of baked into the #define.
#define IThumbnailProviderIid "e357fccd-a995-4576-b01f-234630154e96"
; CLSID of our BananaThumbnailProvider class (Viewer.ThumbnailProvider
; project) - must match its [Guid(...)] attribute exactly.
#define ThumbnailProviderClsid "327b8523-1a5d-4c8d-9d60-611a8acf1572"

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
    HasThumbnailHandler: Boolean; // psd/procreate/sai2/icns - formats Windows has no built-in decoder for
    CheckBox: TNewCheckBox;
  end;

var
  FormatsPage: TWizardPage;
  Formats: array of TFormatEntry;

procedure AddFormat(Ext, Caption, Icon: String; HasThumbnailHandler: Boolean);
var
  i: Integer;
begin
  i := GetArrayLength(Formats);
  SetArrayLength(Formats, i + 1);
  Formats[i].Ext := Ext;
  Formats[i].Caption := Caption;
  Formats[i].Icon := Icon;
  Formats[i].HasThumbnailHandler := HasThumbnailHandler;
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
  AddFormat('.png', 'PNG', 'png.bmp', False);
  AddFormat('.jpg', 'JPG', 'jpg.bmp', False);
  AddFormat('.jpeg', 'JPEG', 'jpg.bmp', False);
  AddFormat('.jfif', 'JFIF', 'jfif.bmp', False);
  AddFormat('.bmp', 'BMP', 'bmp.bmp', False);
  AddFormat('.tif', 'TIF', 'tif.bmp', False);
  AddFormat('.tiff', 'TIFF', 'tif.bmp', False);
  AddFormat('.gif', 'GIF (анимация)', 'gif.bmp', False);
  AddFormat('.webp', 'WEBP', 'webp.bmp', False);
  AddFormat('.ico', 'ICO', 'ico.bmp', False);
  AddFormat('.icns', 'ICNS', 'icns.bmp', True);
  AddFormat('.psd', 'PSD', 'psd.bmp', True);
  AddFormat('.procreate', 'Procreate', 'procreate.bmp', True);
  AddFormat('.sai2', 'SAI2', 'sai2.bmp', True);
  AddFormat('.kra', 'Krita', 'kra.bmp', False);
  AddFormat('.clip', 'CLIP STUDIO', 'clip.bmp', False);
  AddFormat('.avif', 'AVIF', 'avif.bmp', False);

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
    if Formats[i].HasThumbnailHandler then
      Formats[i].CheckBox.Caption := Formats[i].Ext + '  (' + Formats[i].Caption + ', +миниатюры)'
    else
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

function AnyThumbnailFormatChecked: Boolean;
var
  i: Integer;
begin
  Result := False;
  for i := 0 to GetArrayLength(Formats) - 1 do
    if Formats[i].HasThumbnailHandler and Formats[i].CheckBox.Checked then
    begin
      Result := True;
      Exit;
    end;
end;

// The same per-format checkboxes drive two independent registrations:
// FileAssociations (what "Open with BananaView" / default-app offers) and,
// for the four formats Windows has no built-in thumbnail decoder for, the
// Explorer ShellEx thumbnail handler pointer. Both keyed off Checked state.
//
// GUIDs are built as '{' + '{#Macro}' + '}' rather than baked into the
// #define with braces already in them: a literal {xxx-xxx} inside a
// declarative section value would be misparsed as an (unknown) Inno runtime
// constant reference. Pascal string literals aren't runtime-{}-expanded, so
// this concatenation is the simplest way to get a literal curly-braced GUID
// string here.
procedure RegisterCheckedFormats;
var
  i: Integer;
  ShellExKey, Clsid: String;
begin
  Clsid := '{' + '{#ThumbnailProviderClsid}' + '}';

  for i := 0 to GetArrayLength(Formats) - 1 do
  begin
    if Formats[i].CheckBox.Checked then
      RegWriteStringValue(HKLM, 'Software\{#MyAppName}\Capabilities\FileAssociations', Formats[i].Ext, 'BananaView.Image')
    else
      RegDeleteValue(HKLM, 'Software\{#MyAppName}\Capabilities\FileAssociations', Formats[i].Ext);

    if Formats[i].HasThumbnailHandler then
    begin
      ShellExKey := 'Software\Classes\' + Formats[i].Ext + '\ShellEx\{' + '{#IThumbnailProviderIid}' + '}';
      if Formats[i].CheckBox.Checked then
        RegWriteStringValue(HKLM, ShellExKey, '', Clsid)
      else
        RegDeleteKeyIncludingSubkeys(HKLM, ShellExKey);
    end;
  end;
end;

procedure RegisterThumbnailHandlerDll;
var
  ResultCode: Integer;
begin
  if not AnyThumbnailFormatChecked then Exit;
  Exec(ExpandConstant('{sys}\regsvr32.exe'), '/s "' + ExpandConstant('{app}\{#MyAppThumbHostName}') + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    RegisterCheckedFormats;
    RegisterThumbnailHandlerDll;
    // SHCNE_ASSOCCHANGED = 0x8000000, SHCNF_IDLIST = 0 - tells Explorer to
    // pick up the new file associations/thumbnail handlers immediately, no
    // logoff/reboot needed.
    SHChangeNotify($8000000, 0, 0, 0);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
  DllPath: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    DllPath := ExpandConstant('{app}\{#MyAppThumbHostName}');
    if FileExists(DllPath) then
      Exec(ExpandConstant('{sys}\regsvr32.exe'), '/u /s "' + DllPath + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Classes\.psd\ShellEx\{' + '{#IThumbnailProviderIid}' + '}');
    RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Classes\.procreate\ShellEx\{' + '{#IThumbnailProviderIid}' + '}');
    RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Classes\.sai2\ShellEx\{' + '{#IThumbnailProviderIid}' + '}');
    RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Classes\.icns\ShellEx\{' + '{#IThumbnailProviderIid}' + '}');
  end;
end;
