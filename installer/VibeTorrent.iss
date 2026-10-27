; VibeTorrent installer. Build with:
;   ISCC.exe /DAppVersion=1.1.0 /DSourceDir=... /DOutputDir=... installer\VibeTorrent.iss
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
AppName=VibeTorrent
AppVersion={#AppVersion}
AppVerName=VibeTorrent {#AppVersion}
AppPublisher=VibeTorrent
AppPublisherURL=https://github.com/Cahard/VibeTorrent
AppSupportURL=https://github.com/Cahard/VibeTorrent/issues
AppUpdatesURL=https://github.com/Cahard/VibeTorrent/releases
DefaultDirName={localappdata}\Programs\VibeTorrent
DefaultGroupName=VibeTorrent
DisableProgramGroupPage=yes
DisableDirPage=no
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=VibeTorrent-Setup-{#AppVersion}
SetupIconFile=..\src\VibeTorrent.App\Assets\App.ico
UninstallDisplayIcon={app}\VibeTorrent.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany=VibeTorrent
VersionInfoDescription=VibeTorrent installer
VersionInfoProductName=VibeTorrent
VersionInfoProductVersion={#AppVersion}
AppMutex=Local\VibeTorrent.Session.v1

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associate"; Description: "Associate .torrent files and magnet links with VibeTorrent"; GroupDescription: "Windows integration:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\VibeTorrent"; Filename: "{app}\VibeTorrent.exe"
Name: "{group}\Uninstall VibeTorrent"; Filename: "{uninstallexe}"
Name: "{autodesktop}\VibeTorrent"; Filename: "{app}\VibeTorrent.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\.torrent"; ValueType: string; ValueName: ""; ValueData: "VibeTorrent.Torrent"; Flags: uninsdeletevalue; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\VibeTorrent.Torrent"; ValueType: string; ValueName: ""; ValueData: "VibeTorrent torrent"; Flags: uninsdeletekey; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\VibeTorrent.Torrent\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\VibeTorrent.exe,0"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\VibeTorrent.Torrent\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VibeTorrent.exe"" ""%1"""; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet"; ValueType: string; ValueName: ""; ValueData: "URL:VibeTorrent magnet protocol"; Flags: uninsdeletevalue; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\VibeTorrent.exe,0"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VibeTorrent.exe"" ""%1"""; Tasks: associate

[Run]
Filename: "{app}\VibeTorrent.exe"; Description: "{cm:LaunchProgram,VibeTorrent}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
