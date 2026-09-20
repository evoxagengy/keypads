#define MyAppName "Switch Keypad"
#define MyAppVersion "0.3.0"
#define MyAppPublisher "EVOX"
#define MyAppExeName "SwitchKeypad.exe"

[Setup]
AppId={{753BA45D-42E3-4FC0-B765-946E0DE10FB1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Switch Keypad
DefaultGroupName=Switch Keypad
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
DisableReadyPage=no
AllowNoIcons=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\..\DIST
OutputBaseFilename=SwitchKeypad-Setup-v{#MyAppVersion}
SetupIconFile=..\src\SwitchKeypad\Assets\Brand\SwitchKeypad.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
UninstallFilesDir={app}\Uninstall
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
WizardImageFile=wizard-large.bmp
WizardSmallImageFile=wizard-small.bmp
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
RestartIfNeededByRun=no
SetupLogging=yes
UsePreviousAppDir=yes
UsePreviousTasks=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked
Name: "autostart"; Description: "Iniciar o Switch Keypad com o Windows"; GroupDescription: "Inicialização:"; Flags: unchecked

[Files]
Source: "..\build\publish\SwitchKeypad.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Switch Keypad"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Switch Keypad"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SwitchKeypad"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir o Switch Keypad"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
