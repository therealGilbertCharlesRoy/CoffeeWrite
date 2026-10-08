#ifndef AppVersion
  #define AppVersion "0.1.0-dev"
#endif
#ifndef PublishDir
  #define PublishDir "..\..\src\Screenwriter.App\bin\Release\net10.0\win-x64\publish"
#endif

#define AppName "Draftroom"
#define AppExe "Screenwriter.App.exe"

[Setup]
AppId={{C8269930-9C89-4E02-B799-641F005E690B}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Draftroom
DefaultDirName={localappdata}\Programs\Draftroom
DefaultGroupName=Draftroom
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\..\artifacts\windows
OutputBaseFilename=Draftroom-Setup-{#AppVersion}-win-x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\..\src\Screenwriter.App\Assets\avalonia-logo.ico
CloseApplications=force
RestartApplications=no
Uninstallable=yes

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\google-drive-oauth.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\posthog-analytics.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Draftroom"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\Draftroom"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch Draftroom"; Flags: nowait postinstall skipifsilent
