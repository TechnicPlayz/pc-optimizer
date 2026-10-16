
#ifndef AppVersion
  #define AppVersion "1.0.1"
#endif

[Setup]
AppId={{B7C2A9E4-5D1F-4C8A-9E3B-2F6A1D7C0E55}
AppName=Velora PC
AppVersion={#AppVersion}
AppPublisher=Velora
AppSupportURL=https://github.com/TechnicPlayz/pc-optimizer/issues
AppUpdatesURL=https://github.com/TechnicPlayz/pc-optimizer/releases
DefaultDirName={autopf}\Velora PC
DefaultGroupName=Velora PC
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=VeloraPC-Setup-{#AppVersion}
SetupIconFile=..\src\VeloraPC\Assets\app.ico
UninstallDisplayIcon={app}\VeloraPC.exe
UninstallDisplayName=Velora PC
LicenseFile=..\EULA.txt
InfoBeforeFile=..\PRIVACY.txt
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "..\publish\VeloraPC.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Velora PC"; Filename: "{app}\VeloraPC.exe"
Name: "{autodesktop}\Velora PC"; Filename: "{app}\VeloraPC.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "VeloraPC"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\VeloraPC.exe"; Description: "Launch Velora PC"; Flags: nowait postinstall skipifsilent
