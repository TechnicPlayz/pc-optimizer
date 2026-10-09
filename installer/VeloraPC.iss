; Inno Setup script for Velora PC. Build: ISCC /DAppVersion=1.0.0 installer\VeloraPC.iss
#ifndef AppVersion
  #define AppVersion "1.0.0"
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
; Remove the optional "Start with Windows" entry on uninstall
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "VeloraPC"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\VeloraPC.exe"; Description: "Launch Velora PC"; Flags: nowait postinstall skipifsilent
; After a silent in-app update, reopen Velora automatically
Filename: "{app}\VeloraPC.exe"; Flags: nowait; Check: WizardSilent

[UninstallRun]
; Remove the optional weekly maintenance task on uninstall
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""Velora PC\Weekly Maintenance"" /F"; Flags: runhidden; RunOnceId: "RemoveMaintenanceTask"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\VeloraPC"
