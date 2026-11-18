; SperryDev.Logger Viewer - Windows installer (Inno Setup)
;
; Installs the SperryDev.Logger.Viewer console client (reads the logger named pipe) for the
; current user's machine: Program Files install, Start menu shortcut, uninstaller.
;
; Build (from repo root):
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" deploy\windows\SperryDev.Logger.Viewer.iss ^
;     /DAppVersion=0.1.0 /DViewerPublishDir=C:\path\to\viewer\publish
;
; The installer is unsigned. SmartScreen warnings are expected until a certificate exists.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef ViewerPublishDir
  #define ViewerPublishDir "..\..\publish\viewer"
#endif

#define AppName "SperryDev Logger Viewer"
#define AppExe "SperryDev.Logger.Viewer.exe"

[Setup]
AppId={{6F0C2E7A-3B1D-4E55-9A8E-5D2B7C1F4A10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Richard Sperry
DefaultDirName={autopf}\SperryDev.Logger.Viewer
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=bin
OutputBaseFilename=SperryDev.Logger.Viewer-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
ChangesEnvironment=yes

[Files]
Source: "{#ViewerPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent unchecked

[Code]
const
  MachineEnvKey = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';

// Adds {app} to the machine-wide PATH (HKLM) unless it is already present.
procedure AddAppToPath();
var
  Path, Dir: string;
begin
  Dir := ExpandConstant('{app}');
  if not RegQueryStringValue(HKLM, MachineEnvKey, 'Path', Path) then
    Path := '';
  if Pos(';' + Uppercase(Dir) + ';', ';' + Uppercase(Path) + ';') > 0 then
    Exit;
  if (Path <> '') and (Path[Length(Path)] <> ';') then
    Path := Path + ';';
  Path := Path + Dir;
  RegWriteExpandStringValue(HKLM, MachineEnvKey, 'Path', Path);
end;

// Removes {app} from the machine-wide PATH on uninstall.
procedure RemoveAppFromPath();
var
  Path, Dir: string;
begin
  Dir := ExpandConstant('{app}');
  if not RegQueryStringValue(HKLM, MachineEnvKey, 'Path', Path) then
    Exit;
  StringChangeEx(Path, ';' + Dir, '', False);
  StringChangeEx(Path, Dir + ';', '', False);
  StringChangeEx(Path, Dir, '', False);
  RegWriteExpandStringValue(HKLM, MachineEnvKey, 'Path', Path);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    AddAppToPath();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RemoveAppFromPath();
end;
