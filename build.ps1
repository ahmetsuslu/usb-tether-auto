<#
.SYNOPSIS
    USB Tether Auto'yu tek dosyalik bir .exe olarak derler.
.DESCRIPTION
    Varsayilan: self-contained (kendi kendine yeten) exe. .NET kurulu olmayan
    bir makinede de calisir, dosya buyuktur.
    -Lite: framework-dependent exe. Cok kucuktur ama makinede
    ".NET Desktop Runtime 9" kurulu olmasini gerektirir.
.EXAMPLE
    .\build.ps1
.EXAMPLE
    .\build.ps1 -Lite
#>
[CmdletBinding()]
param(
    [switch]$Lite,
    [switch]$NoInstaller,
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'

# Bazi cagirma bicimlerinde $PSScriptRoot bos gelebiliyor.
$root = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($root)) { $root = Split-Path -Parent $MyInvocation.MyCommand.Definition }
if ([string]::IsNullOrWhiteSpace($root)) { $root = (Get-Location).Path }

if ([string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir = Join-Path $root 'dist' }
$project = Join-Path $root 'src\UsbTetherAuto\UsbTetherAuto.csproj'

# Calisan bir ornek varsa cikti dosyasi kilitli kalir. Surec olduktan sonra da
# kilit bir sure devam edebiliyor, o yuzden dosya yazilabilir olana kadar bekle.
function Wait-FileWritable {
    param([string]$Path, [int]$TimeoutSeconds = 20)
    if (-not (Test-Path $Path)) { return $true }
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $fs = [System.IO.File]::Open($Path, 'Open', 'ReadWrite', 'None')
            $fs.Close()
            return $true
        }
        catch { Start-Sleep -Milliseconds 400 }
    }
    return $false
}

$exePath = Join-Path $OutputDir 'UsbTetherAuto.exe'
$running = Get-Process UsbTetherAuto -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Calisan UsbTetherAuto kapatiliyor..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
}
if (-not (Wait-FileWritable -Path $exePath)) {
    throw "Cikti dosyasi hala kilitli: $exePath"
}

$common = @(
    'publish', $project,
    '-c', 'Release',
    '-r', 'win-x64',
    '-o', $OutputDir,
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=none',
    '--nologo'
)

if ($Lite) {
    Write-Host "Derleniyor: framework-dependent (.NET 9 Desktop Runtime gerekir)" -ForegroundColor Cyan
    $args = $common + @('--self-contained', 'false')
} else {
    Write-Host "Derleniyor: self-contained (tasinabilir, .NET gerektirmez)" -ForegroundColor Cyan
    $args = $common + @('--self-contained', 'true', '-p:EnableCompressionInSingleFile=true')
}

& dotnet @args
if ($LASTEXITCODE -ne 0) { throw "Derleme basarisiz (exit $LASTEXITCODE)." }

if (-not (Test-Path $exePath)) { throw "Beklenen cikti bulunamadi: $exePath" }

$sizeMb = [Math]::Round((Get-Item $exePath).Length / 1MB, 1)
Write-Host ""
Write-Host "Hazir: $exePath  ($sizeMb MB)" -ForegroundColor Green

# ---- kurulum dosyasi (Inno Setup) ----
if (-not $NoInstaller) {
    $iscc = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

    if (-not $iscc) {
        Write-Host ""
        Write-Host "Inno Setup bulunamadi, kurulum dosyasi uretilmedi." -ForegroundColor Yellow
        Write-Host "Kurmak icin:  winget install JRSoftware.InnoSetup"
    } else {
        Write-Host ""
        Write-Host "Kurulum dosyasi derleniyor..." -ForegroundColor Cyan
        $iss = Join-Path $root 'installer\UsbTetherAuto.iss'
        & $iscc /Q $iss
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup derlemesi basarisiz (exit $LASTEXITCODE)." }

        $setup = Get-ChildItem (Join-Path $OutputDir 'UsbTetherAuto-Setup-*.exe') |
                 Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($setup) {
            $setupMb = [Math]::Round($setup.Length / 1MB, 1)
            Write-Host "Hazir: $($setup.FullName)  ($setupMb MB)" -ForegroundColor Green
        }
    }
}

Write-Host ""
Write-Host "Kurulum dosyasini calistirmak yeterli; gerisini uygulama anlatir."
