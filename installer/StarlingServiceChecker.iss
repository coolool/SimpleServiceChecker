#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef AppVersionNumeric
  #define AppVersionNumeric "1.0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif
#ifndef WebView2Bootstrapper
  #define WebView2Bootstrapper "..\artifacts\prerequisites\MicrosoftEdgeWebview2Setup.exe"
#endif
#ifndef UpdateLocationUrl
  #define UpdateLocationUrl "https://github.com/coolool/SimpleServiceChecker/releases"
#endif

[Setup]
AppId={{D2E88890-062E-4821-A5F5-FDF1D46EDD9C}
AppName=Starling Service Checker
AppVersion={#AppVersion}
AppVerName=Starling Service Checker {#AppVersion}
AppPublisher=Spectrum Telecom Group Ltd.
AppUpdatesURL={#UpdateLocationUrl}
DefaultDirName={localappdata}\Programs\Starling Service Checker
DefaultGroupName=Starling Service Checker
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=StarlingServiceChecker-Setup-{#AppVersion}
SetupIconFile=..\src\SimpleServiceChecker.App\Assets\StarlingChecker.ico
UninstallDisplayIcon={app}\StarlingServiceChecker.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=StarlingServiceChecker.exe
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
VersionInfoCompany=Spectrum Telecom Group Ltd.
VersionInfoDescription=Starling Service Checker Installer
VersionInfoProductName=Starling Service Checker
VersionInfoProductVersion={#AppVersion}
VersionInfoVersion={#AppVersionNumeric}
#ifdef SignToolName
SignTool={#SignToolName}
SignedUninstaller=yes
#else
SignedUninstaller=no
#endif

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#WebView2Bootstrapper}"; DestDir: "{tmp}"; DestName: "MicrosoftEdgeWebview2Setup.exe"; Flags: deleteafterinstall; Check: WebView2RuntimeMissing

[Icons]
Name: "{autoprograms}\Starling Service Checker"; Filename: "{app}\StarlingServiceChecker.exe"; WorkingDir: "{app}"; AppUserModelID: "Spectrum.StarlingServiceChecker"
Name: "{autodesktop}\Starling Service Checker"; Filename: "{app}\StarlingServiceChecker.exe"; WorkingDir: "{app}"; AppUserModelID: "Spectrum.StarlingServiceChecker"; Tasks: desktopicon

[Run]
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Installing Microsoft Edge WebView2 Runtime…"; Flags: runhidden waituntilterminated; Check: WebView2RuntimeMissing; AfterInstall: VerifyWebView2Runtime
Filename: "{app}\StarlingServiceChecker.exe"; Description: "Launch Starling Service Checker"; WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent; Check: WebView2RuntimeInstalled

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\SimpleServiceChecker"; Check: RemoveUserDataOnUninstall

[Code]
const
  WebView2ClientKey = 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

var
  RemoveUserData: Boolean;

function HasWebView2Version(const RootKey: Integer): Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(RootKey, WebView2ClientKey, 'pv', Version) and
    (Trim(Version) <> '') and (CompareText(Trim(Version), '0.0.0.0') <> 0);
end;

function WebView2RuntimeInstalled: Boolean;
begin
  Result := HasWebView2Version(HKCU) or HasWebView2Version(HKLM32) or HasWebView2Version(HKLM64);
end;

function WebView2RuntimeMissing: Boolean;
begin
  Result := not WebView2RuntimeInstalled;
end;

procedure VerifyWebView2Runtime;
begin
  if WebView2RuntimeMissing then
    MsgBox(
      'Microsoft Edge WebView2 Runtime could not be installed.' + #13#10 + #13#10 +
      'Connect this computer to the internet and run this installer again. If installation is blocked, contact your IT support or download the Evergreen Runtime directly from Microsoft:' + #13#10 + #13#10 +
      'https://go.microsoft.com/fwlink/p/?LinkId=2124703',
      mbError, MB_OK);
end;

function RemoveUserDataOnUninstall: Boolean;
begin
  Result := RemoveUserData;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RemoveUserData := CompareText(ExpandConstant('{param:REMOVEUSERDATA|0}'), '1') = 0;
    if (not UninstallSilent) and (not RemoveUserData) then
      RemoveUserData := MsgBox(
        'Also remove this Windows user''s saved Google API key, coverage workbooks, and local application data?' + #13#10 + #13#10 +
        'Choose No if Starling Service Checker may be installed again on this computer.',
        mbConfirmation, MB_YESNO) = IDYES;
  end;
end;
