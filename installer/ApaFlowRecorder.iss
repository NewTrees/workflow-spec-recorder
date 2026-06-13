#define MyAppName "Workflow Spec Recorder"
#define MyAppVersion "0.2.8"
#define MyAppPublisher "Yumin"
#define MyAppExeName "ApaFlowRecorder.Desktop.exe"

[Setup]
AppId={{9F14CF15-783F-47C7-8B0B-65DD72B76555}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\WorkflowSpecRecorder
DefaultGroupName={#MyAppName}
DisableDirPage=no
DisableProgramGroupPage=no
OutputDir=..\dist\installer
OutputBaseFilename=WorkflowSpecRecorder-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
SetupLogging=yes
SetupIconFile=..\src\ApaFlowRecorder.Desktop\Assets\AppIcon.ico

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "..\dist\ApaFlowRecorderSelfContained\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\打开 Chrome 扩展目录"; Filename: "{app}\extension"; WorkingDir: "{app}\extension"
Name: "{group}\使用说明"; Filename: "{app}\先看我-WorkflowSpecRecorder使用说明.txt"; WorkingDir: "{app}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\extension"; Description: "打开 Chrome 扩展目录，便于在 chrome://extensions 中加载"; Flags: shellexec postinstall skipifsilent unchecked

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
