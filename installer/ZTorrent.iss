; ZTorrent installer. Build with:
;   ISCC.exe /DAppVersion=1.1.0 /DSourceDir=... /DOutputDir=... installer\ZTorrent.iss
#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64-selfcontained"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{8F3C2A5E-7B1D-4C9A-9E2F-6D4B8C1A7E30}
AppName=ZTorrent
AppVersion={#AppVersion}
AppVerName=ZTorrent {#AppVersion}
AppPublisher=ZTorrent
AppPublisherURL=https://github.com/Cahard/ZTorrent
AppSupportURL=https://github.com/Cahard/ZTorrent/issues
AppUpdatesURL=https://github.com/Cahard/ZTorrent/releases
DefaultDirName={localappdata}\Programs\ZTorrent
UsePreviousAppDir=no
DefaultGroupName=ZTorrent
DisableProgramGroupPage=yes
DisableDirPage=no
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=ZTorrent-Setup-{#AppVersion}
SetupIconFile=..\src\ZTorrent.App\Assets\App.ico
UninstallDisplayIcon={app}\ZTorrent.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany=ZTorrent
VersionInfoDescription=ZTorrent installer
VersionInfoProductName=ZTorrent
VersionInfoProductVersion={#AppVersion}
AppMutex=Local\ZTorrent.Session.v1

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associate"; Description: "Associate .torrent files and magnet links with ZTorrent"; GroupDescription: "Windows integration:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ZTorrent"; Filename: "{app}\ZTorrent.exe"
Name: "{group}\Uninstall ZTorrent"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ZTorrent"; Filename: "{app}\ZTorrent.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\.torrent"; ValueType: string; ValueName: ""; ValueData: "ZTorrent.Torrent"; Flags: uninsdeletevalue; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\ZTorrent.Torrent"; ValueType: string; ValueName: ""; ValueData: "ZTorrent torrent"; Flags: uninsdeletekey; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\ZTorrent.Torrent\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\ZTorrent.exe,0"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\ZTorrent.Torrent\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\ZTorrent.exe"" ""%1"""; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet"; ValueType: string; ValueName: ""; ValueData: "URL:ZTorrent magnet protocol"; Flags: uninsdeletevalue; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\ZTorrent.exe,0"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\ZTorrent.exe"" ""%1"""; Tasks: associate

[Run]
Filename: "{app}\ZTorrent.exe"; Description: "{cm:LaunchProgram,ZTorrent}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
