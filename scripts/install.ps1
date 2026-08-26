<#
    USB tethering izleyicisini Gorev Zamanlayici'ya kaydeder.
    Oturum acildiginda gizli olarak baslar ve surekli calisir.
#>
[CmdletBinding()]
param([string]$TaskName = 'USB Tether Auto')

$ErrorActionPreference = 'Stop'
$here     = Split-Path -Parent $MyInvocation.MyCommand.Path
$launcher = Join-Path $here 'run-hidden.vbs'
if (-not (Test-Path $launcher)) { throw "run-hidden.vbs bulunamadi: $launcher" }

$action = New-ScheduledTaskAction -Execute 'wscript.exe' -Argument ('"{0}"' -f $launcher)
$trigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1)

if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Host "Eski gorev kaldirildi."
}

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
    -Settings $settings -Description 'Telefon USB ile baglaninca otomatik USB tethering acar.' | Out-Null

Start-ScheduledTask -TaskName $TaskName
Write-Host "Kuruldu ve baslatildi: '$TaskName'"
Write-Host "Log: $(Join-Path $env:LOCALAPPDATA 'usb-tether-auto\tether.log')"
