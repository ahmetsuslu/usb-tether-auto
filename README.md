# USB Tether Auto

Android telefonu Windows PC'ye USB ile bagladiginda **internet paylasimini
(USB tethering) otomatik acar**. Her seferinde telefonun menusune girip elle
acmaniz gerekmez. Sistem tepsisinde durur, durumunu simgesinin rengiyle gosterir.

Samsung Galaxy S23 (One UI) uzerinde gelistirildi ve test edildi; `svc usb
setFunctions` destekleyen diger Android cihazlarda da calismasi beklenir.

![tepsi simgeleri](tools/icon-preview.png)

| Simge | Anlami |
|---|---|
| Gri | Telefon bagli degil |
| Mavi | Telefon bagli, tethering kapali (aciliyor) |
| Yesil | Tethering acik ve veri akiyor |
| Amber | Arabirim var ama tasiyici yok - acik gorunuyor, veri akmiyor |
| Kirmizi | adb yok / izin verilmedi / acilamadi |

## Neden bir uygulama degil de PC tarafinda?

Android'de USB tethering'i acmak `TETHER_PRIVILEGED` iznini ister; bu izin
`signature|privileged` seviyesindedir ve normal bir uygulamaya verilmez.
Root'suz bir APK bunu yapamaz. Buna karsilik **ADB shell**'in bu yetkisi vardir.

Bu yuzden mantik telefonda degil, PC tarafinda calisir: PC telefonu gorur ->
`adb` ile tethering'i acar. Telefona hicbir sey kurulmaz; telefon yeniden
baslasa da bir sey degismez, PC acilisinda uygulama kendi baslar.

## Kurulum

1. [Releases](https://github.com/ahmetsuslu/usb-tether-auto/releases) sayfasindan
   `UsbTetherAuto-Setup-x.y.z.exe` dosyasini indirin ve calistirin.
   Yonetici hakki istemez, kullanici bazinda kurar.
2. Kurulum bitince uygulama acilir ve **telefonu hazirlama rehberi** kendiliginden
   gorunur. Rehber, telefonda yapmaniz gereken uc adimi sirayla anlatir ve her
   adim tamamlandikca yaninda yesil isaret cikar.

Rehberi daha sonra tekrar acmak isterseniz ana penceredeki **Kurulum rehberi**
dugmesini kullanin.

### Telefonda yapilacaklar (rehber bunlari anlatir)

1. **Ayarlar > Telefon hakkinda > Yazilim bilgileri** -> "Yapi numarasi"na
   7 kez dokunun. Gelistirici secenekleri acilir.
2. **Ayarlar > Gelistirici secenekleri > USB hata ayiklama** -> acin.
3. Kabloyu takin, telefonun kilidini acin, cikan "USB hata ayiklamaya izin
   verilsin mi?" penceresinde **"Bu bilgisayardan her zaman izin ver"** kutusunu
   isaretleyip izin verin.

> Bu kutuyu isaretlemek onemli; yoksa her yeniden baslatmadan sonra elle onay ister.

`adb.exe` otomatik aranir (Android SDK platform-tools, `C:\platform-tools`,
PATH). Bulunamazsa Ayarlar bolumunden **Gozat...** ile elle secebilir veya
[SDK Platform Tools](https://developer.android.com/tools/releases/platform-tools)
zip'ini indirip `C:\platform-tools` klasorune acabilirsiniz.

> Windows 11 yeni tepsi simgelerini varsayilan olarak **`^` tasma menusune**
> koyar. Simgeyi hep gorunur yapmak icin `^` menusunu acip USB simgesini
> saat cubugunun uzerine surukleyin.

## Kullanim

- Pencereyi kapatmak uygulamayi kapatmaz, tepsiye indirir.
- Tepsi simgesine **cift tiklayinca** pencere geri gelir.
- Tepsi simgesine **sag tiklayinca** durum, "simdi ac", "kapat",
  "otomatik ac" anahtari ve "cikis" cikar.
- Uygulamayi tamamen kapatmak icin tepsi menusunden **Cikis**.
- Kaldirmak icin: Ayarlar > Uygulamalar > Yuklu uygulamalar > USB Tether Auto.

## Nasil calisir

`TetherWatcher` birkac saniyede bir `adb devices` ile cihazi yoklar.
Cihaz hazir oldugunda tethering acik mi diye bakar - `dumpsys usb` ciktisindaki
`mCurrentFunctions` ve `rndis0`/`ncm0` arayuzu. Kapaliysa su yontemleri
sirayla dener:

| Yontem | Komut | Not |
|---|---|---|
| `rndis` | `svc usb setFunctions rndis` | Samsung One UI'da beklenen yol |
| `ncm` | `svc usb setFunctions ncm` | Android 11+ Google yolu |
| `root-rndis` | `su -c 'svc usb setFunctions rndis'` | yalnizca root'lu cihazda |

Calisan yontem `settings.json` icine yazilir, sonraki seferlerde once o denenir.
Ust uste 3 deneme basarisiz olursa telefon cikarilip takilana kadar durur -
boylece sonsuz dongude USB fonksiyonu degistirilmez.

Kullanici verisi `%LOCALAPPDATA%\UsbTetherAuto\` altindadir:
- `tether.log` - kayitlar (1 MB'i gecince kirpilir)
- `settings.json` - ayarlar

## Derleme

`.NET 9 SDK` gerekir. Kurulum dosyasi icin ayrica Inno Setup 6
(`winget install JRSoftware.InnoSetup`).

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Cikti `dist\` altina duser:
- `UsbTetherAuto.exe` (~46 MB, self-contained; hedef makinede .NET gerekmez)
- `UsbTetherAuto-Setup-x.y.z.exe` (~41 MB)

Secenekler:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Lite
```

`-Lite`: framework-dependent, cok daha kucuk exe; makinede **.NET Desktop
Runtime 9** kurulu olmalidir. `-NoInstaller`: yalnizca exe uret.

Tepsi simgelerini yeniden uretmek icin (Pillow gerekir):

```bash
python tools\make_icons.py
```

## Proje yapisi

```
src\UsbTetherAuto\           WinForms uygulamasi (C#, net9.0-windows)
  AdbService.cs                adb calistirma, cihaz/tethering durumu, acma-kapama
  TetherWatcher.cs             arka plan izleme dongusu ve durum makinesi
  TrayApplicationContext.cs    tepsi simgesi ve menusu
  MainForm.cs                  ana pencere
  SetupGuideForm.cs            telefonu hazirlama rehberi (canli durumlu)
installer\UsbTetherAuto.iss  Inno Setup betigi
build.ps1                    exe + kurulum dosyasi uretir -> dist\
tools\make_icons.py          tepsi simgelerini uretir
scripts\                     arayuzsuz PowerShell surumu (asagiya bakin)
```

### scripts\ - arayuzsuz surum

Uygulamadan once yazilan saf PowerShell surumu. Ayni mantigi calistirir ama
arayuzu yoktur; Gorev Zamanlayici ile gizli calisir. Uygulamayi kullaniyorsaniz
buna ihtiyaciniz yok. Tek seferlik hizli bir test icin faydali olabilir:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tether-watch.ps1 -Once
```

## Sorun giderme

**Simge kirmizi, "adb bulunamadi"** - Android SDK platform-tools kurun veya
Ayarlar bolumunden `adb.exe` yolunu elle secin.

**Simge kirmizi, "USB hata ayiklama izni bekleniyor"** - Telefonun kilidini
acin ve ekrandaki izin penceresini onaylayin. Gerekirse Gelistirici
secenekleri > **"USB hata ayiklama yetkilerini iptal et"** deyip yeniden
izin verin.

**Telefon hic gorunmuyor** - Aygit Yoneticisi'nde "Android ADB Interface" var mi
bakin. Yoksa USB hata ayiklama kapalidir; USB modunu (MTP/sarj/MIDI) degistirmek
bunu duzeltmez.

**"Tethering acilamadi"** - Android surumune gore `svc usb setFunctions`
kisitlanmis olabilir. Kayitlarda hangi yontemin neden reddedildigi yazar.
Kalan secenekler:
- Telefona **MacroDroid / Tasker** kurup ADB ile `WRITE_SECURE_SETTINGS`
  izni vermek
  (`adb shell pm grant <paket> android.permission.WRITE_SECURE_SETTINGS`)
  ve "Power connected" tetigine tethering aksiyonunu baglamak.
- Root varsa `root-rndis` yontemi zaten sirada.

**Simge amber: "Acik gorunuyor ama veri akmiyor"** - Telefonu yeniden baslatin.
Bu, pratikte gorulmus gercek bir ariza: bir Android surum yukseltmesinden sonra
telefonda `usb0` adinda sahipsiz bir arabirim kalabiliyor. Android'in tethering
yapilandirmasi USB arabirimlerini `[usb\d, rndis\d]` sirasiyla aradigi icin
IP'yi ve DHCP sunucusunu o olu arabirime bagliyor; gercek gadget arabirimi
(`rndis0`) ise `NO-CARRIER` durumunda bekliyor. Sonuc: telefon da PC de
"bagli" der, Windows `169.254.x.x` (APIPA) alir, iki yonde de sifir paket akar.
Telefonu yeniden baslatmak kalinti arabirimi siler ve paylasim duzgun arabirime
baglanir. Sorunu teshis etmek icin:

```bash
adb shell ip -o link | findstr "rndis ncm usb"
```

`LOWER_UP` goruyorsaniz tasiyici vardir; `NO-CARRIER` goruyorsaniz yoktur.

**Tethering acilirken kisa bir kopma oluyor** - Normaldir. `setFunctions`
USB konfigurasyonunu degistirdigi icin adb baglantisi dusup geri gelir;
uygulama bunu bekler.

## Lisans

[MIT](LICENSE)

---

## In English

Automatically enables USB tethering on an Android phone when you plug it into
a Windows PC. Enabling tethering requires the privileged `TETHER_PRIVILEGED`
permission, which no ordinary APK can hold - but the ADB shell can. So the
logic lives on the PC: a tray app watches for the device via `adb` and turns
tethering on (`svc usb setFunctions rndis`, falling back to `ncm` and rooted
`su`). Nothing is installed on the phone; you only need USB debugging enabled
once. Grab the installer from
[Releases](https://github.com/ahmetsuslu/usb-tether-auto/releases).
UI is in Turkish.
