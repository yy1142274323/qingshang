; 清商播放器 —— 安装脚本（Inno Setup 6）
; 编译：ISCC.exe installer\setup.iss

#define MyAppName "清商音乐播放器"
#define MyAppShortName "清商"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "QianQian JingTing"
#define MyAppExeName "QingShang.exe"

[Setup]
AppId={{8F3A2C1E-7B4D-4A6F-9C2E-1D5B8A9E3F01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppComments=从零手写的本地音乐播放器，致敬经典清商。
DefaultDirName={autopf}\QingShang
DefaultGroupName={#MyAppShortName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
OutputDir=..\dist
OutputBaseFilename=QingShang-Setup-{#MyAppVersion}
SetupIconFile=..\src\QingShang\Assets\app.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x86 x64
MinVersion=6.1sp1
DisableWelcomePage=no
LicenseFile=

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked


[Files]
Source: "..\dist\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\{#MyAppShortName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\Uninstall {#MyAppShortName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppShortName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]

