; Inno Setup script for Sketch's installer (SketchSetup-<version>.exe). Built by Setup-Olustur.cmd, which first
; publishes the program with its own .NET runtime into artifacts\publish\win-x64.
;
; Kept plain on purpose so that antivirus programs have nothing to object to: a standard Inno Setup installer,
; no packers or obfuscation, ordinary files copied to the user's own program folder, no administrator rights,
; nothing downloaded during setup. (Windows SmartScreen still shows "unknown publisher" for any unsigned program.)

#define AppName "Sketch"
#define AppVersion "1.0.0"
#define AppPublisher "Bahaddin Kocataş"
#define AppExe "Sketch.App.exe"
#define PublishDir "..\artifacts\publish\win-x64"

[Setup]
AppId={{27B63D61-236A-4B7D-805A-4A4FC5449B9F}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup
; Per-user install (%LOCALAPPDATA%\Programs\Sketch): no administrator prompt.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
SetupIconFile=..\src\Sketch.App\Resources\Sketch.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=SketchSetup-{#AppVersion}

[Languages]
; English only: the program itself is in English.
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
