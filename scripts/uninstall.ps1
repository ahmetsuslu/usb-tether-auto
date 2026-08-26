[CmdletBinding()]
param([string]$TaskName = 'USB Tether Auto')

$ErrorActionPreference = 'Stop'
if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Stop-ScheduledTask  -TaskName $TaskName -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Host "Kaldirildi: '$TaskName'"
} else {
    Write-Host "Gorev zaten yok: '$TaskName'"
}
# Halen calisan izleyici surecini de kapat
Get-CimInstance Win32_Process -Filter "Name='powershell.exe' OR Name='wscript.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -like '*tether-watch*' -or $_.CommandLine -like '*run-hidden.vbs*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
