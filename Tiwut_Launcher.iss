[Setup]
AppId={{B5A34A6E-78A7-4E9A-8A4E-064B4E3881E9}
AppName=Tiwut Launcher
AppVersion=1.1.1
AppPublisher=Tiwut
AppPublisherURL=https://tiwut.org
AppSupportURL=https://tiwut.org
AppUpdatesURL=https://tiwut.org
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Tiwut Launcher
DefaultGroupName=Tiwut Launcher
DisableProgramGroupPage=yes
Compression=lzma
SolidCompression=yes
OutputDir=C:\Users\User\Desktop
OutputBaseFilename=Inno_Setup_Tiwut_Launcher
SetupIconFile=C:/Users/User/Documents/GitHub/Tiwut-Installer/app.ico
LicenseFile=C:/Users/User/Downloads/LICENSE.txt
WizardStyle=modern

[Files]
Source: "C:/Users/User/Downloads/dist/*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Tiwut Launcher"; Filename: "{app}\TiwutLauncher.exe"
Name: "{userdesktop}\Tiwut Launcher"; Filename: "{app}\TiwutLauncher.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\TiwutLauncher.exe"; Description: "{cm:LaunchProgram,Tiwut Launcher}"; Flags: nowait postinstall skipifsilent

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Code]
procedure InitializeWizard();
var
  CustomGray: TColor;
  CustomGreen: TColor;
  CustomOrange: TColor;
begin
  CustomGray := $2B2B2B;
  CustomGreen := $4CAF50;
  CustomOrange := $00A5FF;
  WizardForm.Color := CustomGray;
  WizardForm.InnerPage.Color := CustomGray;
  WizardForm.WelcomePage.Color := CustomGray;
  WizardForm.FinishedPage.Color := CustomGray;
  WizardForm.WelcomeLabel1.Font.Color := CustomGreen;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];
  WizardForm.WelcomeLabel2.Font.Color := clWhite;
  WizardForm.FinishedHeadingLabel.Font.Color := CustomGreen;
  WizardForm.FinishedLabel.Font.Color := clWhite;
  WizardForm.PageNameLabel.Font.Color := CustomOrange;
  WizardForm.PageDescriptionLabel.Font.Color := clWhite;
  WizardForm.TasksList.Color := CustomGray;
  WizardForm.TasksList.Font.Color := clWhite;
  WizardForm.MainPanel.Color := CustomGray;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpReady then
  begin
    WizardForm.ReadyMemo.Color := $333333;
    WizardForm.ReadyMemo.Font.Color := $00A5FF;
  end;
end;