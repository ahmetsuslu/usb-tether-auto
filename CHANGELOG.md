# Degisiklik gunlugu

Bu proje [Semantic Versioning](https://semver.org/lang/tr/) kullanir.

## [1.1.0] - 2026-08-26

Son kullaniciya yonelik surum: artik kurulum dosyasi var ve telefon hazirligi
uygulamanin icinden anlatiliyor.

### Eklendi
- **Kurulum dosyasi** (Inno Setup). Yonetici hakki istemez, kullanici bazinda
  kurar; Baslat menusune kisayol koyar, "Yuklu uygulamalar" listesinde gorunur
  ve duzgun bir kaldirici birakir. Kurulum sirasinda "Windows acilisinda
  baslat" ve "masaustune kisayol" secenekleri sunulur.
- **Telefonu hazirlama rehberi** (`SetupGuideForm`). Ilk calistirmada
  kendiliginden acilir, ana penceredeki "Kurulum rehberi" dugmesiyle her zaman
  yeniden acilabilir. Gelistirici secenekleri ve USB hata ayiklama adimlarini
  sirayla anlatir; her adim tamamlandikca yaninda yesil isaret cikar ve alttaki
  satir cihazin canli durumunu gosterir.
- Ana pencerenin altbilgisinde surum numarasi.

### Degisti
- `build.ps1` artik exe'nin yanina kurulum dosyasini da uretiyor
  (`-NoInstaller` ile atlanabilir) ve calisan bir ornek varsa dosya kilidi
  cozulene kadar bekliyor.

## [1.0.0] - 2026-08-26

Ilk calisan surum.

### Eklendi
- Tepside calisan izleyici: `adb devices` ile cihazi yoklar, telefon
  baglaninca USB tethering'i acar.
- Sirayla denenen acma yontemleri (`rndis`, `ncm`, root'lu `su`); calisan
  yontem hatirlanir ve sonraki seferde once o denenir.
- Durumu renkle gosteren USB simgesi (gri / mavi / yesil / kirmizi), tepsi
  menusu ve balon bildirimleri.
- Ayarlar: otomatik acma, Windows ile baslatma, bildirimler, `adb.exe` yolu.
- Canli kayit penceresi ve `%LOCALAPPDATA%\UsbTetherAuto\tether.log`.
- Windows'un acik/koyu temasina uyum, tek ornek kilidi.
- Arayuzsuz PowerShell surumu (`scripts\`).
