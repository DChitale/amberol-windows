; Inno Setup Script for Amberol Windows
; Compile this script using the Inno Setup Compiler (ISCC)

#define MyAppName "Amberol"
#define MyAppVersion "2.0"
#define MyAppPublisher "DChitale"
#define MyAppURL "https://github.com/DChitale/amberol-windows"
#define MyAppExeName "amberol-win.exe"

[Setup]
AppId={{9F6D9712-4C3D-4A23-95D9-20EF240212BF}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; We require admin privileges to check registry and set up file associations properly
PrivilegesRequired=admin
OutputBaseFilename=AmberolSetup
SetupIconFile=d:\Projects\amberol-windows\icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "fileassoc"; Description: "Associate audio files with Amberol"; GroupDescription: "File Associations:"

[Files]
Source: "d:\Projects\amberol-windows\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; File associations for MP3
Root: HKA; Subkey: "Software\Classes\.mp3\OpenWithProgids"; ValueType: string; ValueName: "Amberol.AssocFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
; File associations for WAV
Root: HKA; Subkey: "Software\Classes\.wav\OpenWithProgids"; ValueType: string; ValueName: "Amberol.AssocFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
; File associations for FLAC
Root: HKA; Subkey: "Software\Classes\.flac\OpenWithProgids"; ValueType: string; ValueName: "Amberol.AssocFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
; File associations for OGG
Root: HKA; Subkey: "Software\Classes\.ogg\OpenWithProgids"; ValueType: string; ValueName: "Amberol.AssocFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
; File associations for OPUS
Root: HKA; Subkey: "Software\Classes\.opus\OpenWithProgids"; ValueType: string; ValueName: "Amberol.AssocFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc

; ProgID registration
Root: HKA; Subkey: "Software\Classes\Amberol.AssocFile"; ValueType: string; ValueName: ""; ValueData: "Audio File"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Amberol.AssocFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Amberol.AssocFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent


