<#
.SYNOPSIS
    Telefon USB ile bagladiginda otomatik olarak USB tethering'i acar.
.DESCRIPTION
    Arka planda calisir. adb ile cihazi bekler, baglaninca USB tethering'i
    (RNDIS/NCM) etkinlestirir, cihaz cikarilinca tekrar beklemeye doner.
.PARAMETER Once
    Dongu kurmadan tek seferlik dener ve hangi yontemin calistigini raporlar.
.PARAMETER AdbPath
    adb.exe yolu. Verilmezse otomatik aranir.
#>
[CmdletBinding()]
param(
    [switch]$Once,
    [string]$AdbPath,
    [int]$PollSeconds = 3
)

$ErrorActionPreference = 'Stop'

$StateDir = Join-Path $env:LOCALAPPDATA 'usb-tether-auto'
if (-not (Test-Path $StateDir)) { New-Item -ItemType Directory -Path $StateDir -Force | Out-Null }
$LogFile   = Join-Path $StateDir 'tether.log'
$StateFile = Join-Path $StateDir 'method.txt'

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $line = '{0} [{1}] {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level, $Message
    Write-Verbose $line
    if ($Once) { Write-Host $line }
    try {
        Add-Content -Path $LogFile -Value $line -Encoding utf8
        # log dosyasi 1 MB'i asarsa kirp
        if ((Get-Item $LogFile).Length -gt 1MB) {
            $tail = Get-Content $LogFile -Tail 500
            Set-Content -Path $LogFile -Value $tail -Encoding utf8
        }
    } catch { }
}

function Resolve-Adb {
    param([string]$Hint)
    $candidates = @()
    if ($Hint) { $candidates += $Hint }
    $cmd = Get-Command adb.exe -ErrorAction SilentlyContinue
    if ($cmd) { $candidates += $cmd.Source }
    $candidates += @(
        (Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'),
        (Join-Path $env:USERPROFILE 'AppData\Local\Android\Sdk\platform-tools\adb.exe'),
        'C:\platform-tools\adb.exe',
        (Join-Path ${env:ProgramFiles} 'Android\platform-tools\adb.exe')
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { return (Resolve-Path $c).Path }
    }
    throw "adb.exe bulunamadi. -AdbPath ile yolu verin veya platform-tools'u PATH'e ekleyin."
}

function Invoke-Adb {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$AdbArgs)
    # adb stderr'e yazdiginda PS 5.1 bunu ErrorRecord'a cevirir; ErrorActionPreference
    # 'Stop' oldugu icin script'i dusurur. Burada bastirip duz metne ceviriyoruz.
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'SilentlyContinue'
    try {
        $out = & $script:Adb @AdbArgs 2>&1 | ForEach-Object { $_.ToString() }
        return ($out -join "`n")
    }
    finally {
        $ErrorActionPreference = $prev
        $global:LASTEXITCODE = 0
    }
}

function Get-DeviceState {
    # DIKKAT: cihaz yokken adb "error: no devices/emulators found" yazar; bu metin
    # 'device' alt dizesini icerdigi icin tam esleme sart.
    $s = (Invoke-Adb 'get-state').Trim()
    if ($s -match '(?m)^device$') { return 'device' }
    return 'offline'
}

# Tethering acik mi? Iki bagimsiz sinyale bakiyoruz:
#  1) USB fonksiyonu rndis/ncm mi (dumpsys usb)
#  2) rndis0/ncm0 arayuzu var mi
function Test-Tethered {
    $usb = Invoke-Adb 'shell' 'dumpsys' 'usb'
    if ($usb -match 'mCurrentFunctions\s*[:=]\s*[^\r\n]*(rndis|ncm)') { return $true }
    $links = Invoke-Adb 'shell' 'ip' '-o' 'link'
    if ($links -match '\b(rndis0|ncm0|usb0)\b') { return $true }
    return $false
}

# Denenecek yontemler, sirayla. Calisan yontem diske yazilir, sonraki
# seferlerde once o denenir.
$Methods = [ordered]@{
    'rndis'      = @('shell', 'svc', 'usb', 'setFunctions', 'rndis')
    'ncm'        = @('shell', 'svc', 'usb', 'setFunctions', 'ncm')
    'root-rndis' = @('shell', 'su', '-c', 'svc usb setFunctions rndis')
}

function Wait-DeviceBack {
    param([int]$TimeoutSeconds = 20)
    # setFunctions USB konfigurasyonunu degistirdigi icin adb kisa sureligine kopabilir
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ((Get-DeviceState) -eq 'device') { return $true }
        Start-Sleep -Seconds 1
    }
    return $false
}

function Enable-Tethering {
    $order = @($Methods.Keys)
    if (Test-Path $StateFile) {
        $known = (Get-Content $StateFile -Raw).Trim()
        if ($Methods.Contains($known)) {
            $order = @($known) + ($order | Where-Object { $_ -ne $known })
        }
    }

    foreach ($name in $order) {
        Write-Log "Yontem deneniyor: $name"
        $methodArgs = $Methods[$name]
        $result = Invoke-Adb @methodArgs
        if ($result -match 'not found|Permission denied|Exception|inaccessible') {
            Write-Log "  -> reddedildi: $($result.Trim())" 'WARN'
            continue
        }
        Start-Sleep -Seconds 3
        if (-not (Wait-DeviceBack)) {
            # adb geri gelmedi ama tethering acilmis olabilir (adb fonksiyonu dustu)
            Write-Log "  -> adb geri donmedi; '$name' USB fonksiyonunu degistirmis olabilir." 'WARN'
            return $name
        }
        if (Test-Tethered) {
            Write-Log "  -> BASARILI ($name)"
            Set-Content -Path $StateFile -Value $name -Encoding ascii
            return $name
        }
        Write-Log "  -> etkisiz" 'WARN'
    }
    return $null
}

# ---- ana akis ----
$script:Adb = Resolve-Adb -Hint $AdbPath
Write-Log "Baslatildi. adb: $script:Adb"
Invoke-Adb 'start-server' | Out-Null

if ($Once) {
    if ((Get-DeviceState) -ne 'device') {
        Write-Log "Cihaz bagli degil. Telefonu USB ile takin ve 'USB hata ayiklama'nin acik oldugundan emin olun." 'ERROR'
        exit 1
    }
    if (Test-Tethered) { Write-Log 'Tethering zaten acik.'; exit 0 }
    $m = Enable-Tethering
    if ($m) { exit 0 } else { Write-Log 'Hicbir yontem ise yaramadi. README''deki sorun giderme bolumune bakin.' 'ERROR'; exit 1 }
}

$wasConnected = $false
while ($true) {
    try {
        $state = Get-DeviceState

        if ($state -eq 'device') {
            if (-not $wasConnected) {
                Write-Log 'Cihaz baglandi.'
                $wasConnected = $true
                Start-Sleep -Seconds 2   # cihazin oturmasini bekle
            }
            if (-not (Test-Tethered)) {
                Write-Log 'Tethering kapali, aciliyor...'
                $m = Enable-Tethering
                if (-not $m) {
                    Write-Log 'Acilamadi; 30 sn sonra tekrar denenecek.' 'ERROR'
                    Start-Sleep -Seconds 30
                }
            }
        }
        else {
            if ($wasConnected) {
                Write-Log 'Cihaz ayrildi.'
                $wasConnected = $false
            }
        }
    }
    catch {
        Write-Log "Hata: $($_.Exception.Message)" 'ERROR'
    }
    Start-Sleep -Seconds $PollSeconds
}
