; Gerenciador Final — Inno Setup 6.3+
; Compilar somente apos dotnet publish ter produzido a pasta Distribuicao.
; Nao cria exclusoes de antivirus ou regras de firewall.
#define MyAppName "Gerenciador Final"
#define MyAppVersion GetVersionNumbersString(AddBackslash(SourcePath) + "Distribuicao\GerenciadorFinal.exe")
#define MyAppExeName "GerenciadorFinal.exe"

[Setup]
AppId={{1EA6D294-11B8-4BF4-B0D8-421DC0666CC2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
DefaultDirName={autopf}\Gerenciador Final
DefaultGroupName=Gerenciador Final
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=Instalador
OutputBaseFilename=GerenciadorFinal-Setup-v{#MyAppVersion}-Windows-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=Assets\GerenciadorFinal.ico
SignTool=gf_sign
SignedUninstaller=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=yes
SetupLogging=no
UsePreviousAppDir=yes
UsePreviousTasks=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na area de trabalho (recomendado)"; GroupDescription: "Atalhos:"

[Files]
Source: "Distribuicao\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Gerenciador Final"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Gerenciador Final"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir Gerenciador Final"; Flags: nowait postinstall skipifsilent runasoriginaluser

; IMPORTANTE: A instalacao pede UAC, mas o aplicativo roda como usuario normal
; (manifest asInvoker) e solicita elevacao nas operacoes administrativas.
; Nenhuma exclusao de AVG/Defender ou liberacao de firewall e aplicada.
