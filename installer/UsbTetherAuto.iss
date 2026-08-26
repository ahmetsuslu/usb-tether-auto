; USB Tether Auto - Inno Setup betigi
; Kullanici bazinda kurar (yonetici/UAC gerektirmez), Baslat menusune kisayol
; koyar ve "Yuklu uygulamalar" listesinde gorunur.
;
; Derleme: build.ps1 bunu otomatik cagirir. Elle:
;   "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\UsbTetherAuto.iss

#define AppName        "USB Tether Auto"
#define AppPublisher   "Ahmet Suslu"
#define AppUrl         "https://github.com/ahmetsuslu/usb-tether-auto"
#define AppExeName     "UsbTetherAuto.exe"

; Surum derlenen exe'den okunur; boylece tek yerden (csproj) yonetilir.
; ProductVersion "1.1.0" gibi kisa gelir, VersionInfo ise 4 parca ister.
#define AppVersion    GetStringFileInfo("..\dist\" + AppExeName, "ProductVersion")
#define AppVersion4   GetVersionNumbersString("..\dist\" + AppExeName)

[Setup]
AppId={{15E890E4-6A96-46D6-B605-805A21C4268D}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion4}

; Yonetici hakki istemeyelim: tepside calisan kucuk bir arac icin gereksiz.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
AllowNoIcons=yes

OutputDir=..\dist
OutputBaseFilename=UsbTetherAuto-Setup-{#AppVersion}
SetupIconFile=..\src\UsbTetherAuto\Resources\usb.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Kurulum sirasinda uygulama aciksa kapatilmasini iste (yeniden baslatma sormadan).
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=no

[Languages]
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "{cm:MyStartupTask}"; GroupDescription: "{cm:MyExtraOptions}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:MyExtraOptions}"; Flags: unchecked

[Files]
Source: "..\dist\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; "Windows ile birlikte baslat" gorevi secildiyse, uygulamanin kendi ayariyla
; ayni anahtari yazariz; uygulama icinden de acilip kapatilabilir.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "UsbTetherAuto"; \
    ValueData: """{app}\{#AppExeName}"" --tray"; \
    Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Kullanici verisi (kayitlar + ayarlar) kaldirmada silinsin.
Type: filesandordirs; Name: "{localappdata}\UsbTetherAuto"

[CustomMessages]
tr.MyStartupTask=Windows acilisinda tepside baslat (onerilir)
tr.MyExtraOptions=Ek secenekler:
en.MyStartupTask=Start in the tray when Windows starts (recommended)
en.MyExtraOptions=Additional options:
