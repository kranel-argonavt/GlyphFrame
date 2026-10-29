; One stable identity across releases. Do not derive it from version or Publisher.
#define ProductId "9A07BED0-6720-4A06-8C95-317B14C580A0"
#ifndef ProductVersion
  #error ProductVersion must come from the verified application assembly
#endif
#ifndef PayloadFiles
  #error PayloadFiles must be generated from the verified portable manifest
#endif
#ifndef NativeTermsFile
  #error NativeTermsFile must contain the verified nonoperative draft and full Microsoft references
#endif
#ifndef NativeTermsBundle
  #error NativeTermsBundle must identify the verified implementation-preview profile
#endif
#ifndef NativeNoticesFile
  #error NativeNoticesFile must contain the verified local notices
#endif

[Setup]
AppId={{{#ProductId}}
AppName=GlyphFrame
AppVersion={#ProductVersion}
AppVerName=GlyphFrame {#ProductVersion}
AppPublisher=Nazar Khozin (kranel-argonavt)
AppPublisherURL=https://github.com/kranel-argonavt
AppSupportURL=https://github.com/kranel-argonavt
DefaultDirName={localappdata}\Programs\GlyphFrame
DefaultGroupName=GlyphFrame
PrivilegesRequired=lowest
SetupArchitecture=x64
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19041
DisableDirPage=yes
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
UsePreviousTasks=yes
UninstallDisplayIcon={app}\GlyphFrame.App.exe
SetupIconFile=..\src\GlyphFrame.App\Assets\GlyphFrame.ico
CloseApplications=no
RestartApplications=no
SetupLogging=yes
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
OutputBaseFilename=GlyphFrame-{#ProductVersion}-win-x64-Setup
VersionInfoVersion={#ProductVersion}.0
VersionInfoCompany=Nazar Khozin (kranel-argonavt)
VersionInfoDescription=GlyphFrame per-user installer
Uninstallable=yes
UninstallFilesDir={app}
AllowNoIcons=no
ShowLanguageDialog=no
LicenseFile={#NativeTermsFile}

[Messages]
WizardLicense=Microsoft component review - local preview
LicenseLabel=Nonoperative implementation preview. This is not a legal agreement or public release.
LicenseLabel3=Review the draft and complete Microsoft references below. This acknowledgement is for local verification only; other components retain their separate rights.
LicenseAccepted=I &acknowledge reviewing this nonoperative draft for local verification
LicenseNotAccepted=I &do not acknowledge this preview

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
; Optional inert viewer; temporary only, never a user-data or installed acceptance file.
Source: "{#NativeNoticesFile}"; Flags: dontcopy
#include PayloadFiles

[Icons]
Name: "{userprograms}\GlyphFrame"; Filename: "{app}\GlyphFrame.App.exe"; WorkingDir: "{app}"; IconFilename: "{app}\GlyphFrame.App.exe"; IconIndex: 0
Name: "{userdesktop}\GlyphFrame"; Filename: "{app}\GlyphFrame.App.exe"; WorkingDir: "{app}"; IconFilename: "{app}\GlyphFrame.App.exe"; IconIndex: 0; Tasks: desktopicon

[Run]
Filename: "{app}\GlyphFrame.App.exe"; Description: "Launch GlyphFrame"; Flags: nowait postinstall skipifsilent unchecked

[Code]
#include "NativeTerms.iss"

function InstallationMutex(Param: String): String;
begin
  Result := 'Global\GlyphFrame.Installation.' +
    Lowercase(GetSHA256OfUnicodeString(Uppercase(ExpandFileName(ExpandConstant('{app}\GlyphFrame.App.exe')))));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  PreviousVersion, NextVersion: Int64;
begin
  Result := '';
  { The app directory is initialized here, unlike early AppMutex code constants. }
  if CheckForMutexes(InstallationMutex('')) then
    Result := 'GlyphFrame is running. Close this installed copy and try again. No process will be terminated.'
  else if FileExists(ExpandConstant('{app}\GlyphFrame.App.exe')) then begin
    if not GetPackedVersion(ExpandConstant('{app}\GlyphFrame.App.exe'), PreviousVersion) then
      Result := 'Cannot read the installed GlyphFrame version. Installation has not started.'
    else if StrToVersion('{#ProductVersion}.0', NextVersion) and (PreviousVersion > NextVersion) then
      Result := 'A newer GlyphFrame version is installed. Downgrades are not supported.';
  end;
  if Result = '' then
    Result := PrepareNativeTermsAcknowledgement();
end;

function InitializeUninstall(): Boolean;
begin
  Result := not CheckForMutexes(InstallationMutex(''));
  if not Result then
    SuppressibleMsgBox('GlyphFrame is running. Close this installed copy before uninstalling. No process will be terminated.', mbError, MB_OK, IDOK);
end;

{ No UninstallDelete/AppData/exports cleanup, associations, autorun or automatic updater. }
