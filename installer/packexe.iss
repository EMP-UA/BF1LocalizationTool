; ==============================================================================
; Star Wars: Battlefront (Classic, 2004) - Ukrainian Localization Installer Script
; Автор / Author: EMP_UA (https://github.com/EMP-UA)
; ==============================================================================

#define AppName "Star Wars: Battlefront (Classic, 2004) Українізатор"
#define AppVersion "1.00"
#define AppPublisher "EMP_UA"
#define AppURL "https://emp-ua-site.pages.dev/"
; Унікальний ID проєкту (GUID має починатися з ДВОХ фігурних дужок)
#define AppId "{{09B7E782-B752-4F6A-BA2F-5D7CEAE6D382}}"
; Офіційний ID гри у Steam
#define SteamAppId "1058020"
#define GroupName "Star Wars Battlefront (Classic, 2004) Українізатор"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={code:GetSteamPath}
DefaultGroupName={#GroupName}
DisableProgramGroupPage=yes
DisableDirPage=no
DirExistsWarning=no
CloseApplications=yes
UsePreviousAppDir=no
OutputDir=Output
OutputBaseFilename=SWB_UA_v{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
ukrainian.SelectDirDesc=Виберіть папку, у якій встановлено Star Wars: Battlefront (Classic, 2004).
ukrainian.SelectDirLabel3=Інсталятор встановить локалізацію у відповідну папку.
english.SelectDirDesc=Select the folder where Star Wars: Battlefront (Classic, 2004) is installed.
english.SelectDirLabel3=The installer will place the localization into the respective folder.

[CustomMessages]
ukrainian.LaunchGame=Запустити гру
english.LaunchGame=Launch the game
ukrainian.ViewReadme=Переглянути Readme
english.ViewReadme=View Readme

[Files]
; Папка GameData поруч із компілятором автоматично накладеться на папку з грою
Source: "GameData\*"; DestDir: "{app}\GameData"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Readme.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Registry]
; Записуємо версію в реєстр користувача
Root: HKCU; Subkey: "Software\{#AppPublisher}\{#AppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"; Flags: uninsdeletekey

[Run]
; Опціональний запуск гри після встановлення (без галочки за замовчуванням)
Filename: "steam://rungameid/{#SteamAppId}"; Description: "{cm:LaunchGame}"; Flags: shellexec postinstall nowait skipifsilent unchecked
; Опціональне відкриття Readme після встановлення (без галочки за замовчуванням)
Filename: "{app}\Readme.txt"; Description: "{cm:ViewReadme}"; Flags: postinstall shellexec nowait skipifsilent unchecked

[Code]
var
  GitHubLabel: TNewStaticText;
  ShellExecErrorCode: Integer;

procedure GitHubLabelClick(Sender: TObject);
begin
  ShellExec('open', 'https://github.com/EMP-UA/BF1LocalizationTool/tree/main/installer/packexe.iss', '', '', SW_SHOWNORMAL, ewNoWait, ShellExecErrorCode);
end;

procedure InitializeWizard();
begin
  GitHubLabel := TNewStaticText.Create(WizardForm);
  GitHubLabel.Top := WizardForm.ClientHeight - 28; // Лівий нижній кут
  GitHubLabel.Left := 20;
  GitHubLabel.Anchors := [akLeft, akBottom];
  GitHubLabel.Caption := '🛠 Вихідний код інсталятора (GitHub)';
  GitHubLabel.Font.Color := clHotLight;
  GitHubLabel.Font.Style := [fsUnderline];
  GitHubLabel.Cursor := crHand;
  GitHubLabel.OnClick := @GitHubLabelClick;
  GitHubLabel.Parent := WizardForm;
end;

{ Перевіряє, чи шлях НЕ містить заборонених у Windows символів
  (окрім двокрапки після літери диска, напр. "C:") }
function IsValidWindowsPath(const Path: String): Boolean;
var
  CheckPath: String;
begin
  Result := False;
  if Path = '' then
    Exit;

  CheckPath := Path;
  { Прибираємо "C:" на початку, щоб не плутати з забороненою ':' }
  if (Length(CheckPath) >= 2) and (CheckPath[2] = ':') then
    Delete(CheckPath, 1, 2);

  if (Pos('/', CheckPath) > 0) or (Pos(':', CheckPath) > 0) or
     (Pos('*', CheckPath) > 0) or (Pos('?', CheckPath) > 0) or
     (Pos('"', CheckPath) > 0) or (Pos('<', CheckPath) > 0) or
     (Pos('>', CheckPath) > 0) or (Pos('|', CheckPath) > 0) then
  begin
    Result := False;
    Exit;
  end;

  Result := True;
end;

function GetSteamPath(Param: String): String;
var
  Path: String;
  DefaultResult: String;
begin
  DefaultResult := ExpandConstant('{pf32}\Steam\steamapps\common\Star Wars Battlefront (Classic 2004)');
  Result := DefaultResult;

  { 1. Спроба через конкретну гру.
    InstallLocation тут ВЖЕ є кінцевим шляхом до теки гри. }
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {#SteamAppId}', 'InstallLocation', Path) then
  begin
    Log('GetSteamPath: сире значення InstallLocation = "' + Path + '"');
    Path := Trim(Path);
    StringChange(Path, '"', '');
    StringChange(Path, '/', '\');

    if (Path <> '') and (Path[Length(Path)] = '\') then
      Path := Copy(Path, 1, Length(Path) - 1);

    if IsValidWindowsPath(Path) then
    begin
      Log('GetSteamPath: використано шлях з Uninstall-ключа гри: ' + Path);
      Result := Path;
      Exit;
    end
    else
      Log('GetSteamPath: шлях з Uninstall-ключа НЕВАЛІДНИЙ, пропускаємо: ' + Path);
  end
  else
    Log('GetSteamPath: ключ Uninstall\Steam App {#SteamAppId} не знайдено');

  { 2. Спроба через загальний шлях Steam. }
  if RegQueryStringValue(HKEY_CURRENT_USER, 'Software\Valve\Steam', 'SteamPath', Path) then
  begin
    Log('GetSteamPath: сире значення SteamPath = "' + Path + '"');
    Path := Trim(Path);
    StringChange(Path, '"', '');
    StringChange(Path, '/', '\');

    if Path <> '' then
    begin
      Path := AddBackslash(Path) + 'steamapps\common\Star Wars Battlefront (Classic 2004)';
      if IsValidWindowsPath(Path) then
      begin
        Log('GetSteamPath: використано шлях, побудований з SteamPath: ' + Path);
        Result := Path;
        Exit;
      end
      else
        Log('GetSteamPath: побудований шлях НЕВАЛІДНИЙ, пропускаємо: ' + Path);
    end;
  end
  else
    Log('GetSteamPath: ключ HKCU\Software\Valve\Steam не знайдено');

  { 3. Фолбек за замовчуванням }
  Log('GetSteamPath: використано шлях за замовчуванням: ' + DefaultResult);
  Result := DefaultResult;
end;

function InitializeSetup(): Boolean;
var
  OldVersion: String;
begin
  Result := True;
  if RegQueryStringValue(HKCU, 'Software\{#AppPublisher}\{#AppName}', 'Version', OldVersion) then
  begin
    if OldVersion = '{#AppVersion}' then
    begin
      if MsgBox('Українізатор версії ' + OldVersion + ' вже встановлено. Бажаєте перевстановити його?', mbConfirmation, MB_YESNO) = IDNO then
        Result := False;
    end
    else
      MsgBox('Знайдено попередню версію: ' + OldVersion + #13#10 + 'Буде встановлено версію: {#AppVersion}', mbInformation, MB_OK);
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    if not FileExists(ExpandConstant('{app}\GameData\Battlefront.exe')) then
    begin
      if MsgBox('У вказаній папці не знайдено файлів гри (Battlefront.exe).' #13#10#13#10 +
                'Ви впевнені, що хочете встановити файли сюди?', mbConfirmation, MB_YESNO) = IDNO then
        Result := False;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if ActiveLanguage = 'ukrainian' then
      MsgBox('Українізатор успішно видалено.' #13#10#13#10 +
             'УВАГА: Оскільки переклад замінював оригінальні файли, гра зараз не запуститься.' #13#10#13#10 +
             'Щоб відновити оригінальну англійську версію:' #13#10 +
             '1. Відкрийте Steam' #13#10 +
             '2. Натисніть правою кнопкою миші на Star Wars: Battlefront' #13#10 +
             '3. "Властивості" -> "Встановлені файли"' #13#10 +
             '4. "Перевірити цілісність файлів гри"', 
             mbInformation, MB_OK)
    else
      MsgBox('The Ukrainian localization has been successfully removed.' #13#10#13#10 +
             'WARNING: Essential files are missing and the game will not start.' #13#10#13#10 +
             'To restore the original English version:' #13#10 +
             '1. Open Steam' #13#10 +
             '2. Right-click on Star Wars: Battlefront' #13#10 +
             '3. "Properties" -> "Installed Files"' #13#10 +
             '4. "Verify integrity of game files"', 
             mbInformation, MB_OK);
  end;
end;