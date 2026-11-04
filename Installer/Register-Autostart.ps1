$ErrorActionPreference = 'Stop'
$taskName = 'PerformanceOverlay'
$appPath = Join-Path $PSScriptRoot 'PerformanceOverlay.exe'
if (-not (Test-Path -LiteralPath $appPath)) { throw "Application executable not found: $appPath" }
$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$action = New-ScheduledTaskAction -Execute $appPath -WorkingDirectory $PSScriptRoot
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $currentUser
$principal = New-ScheduledTaskPrincipal -UserId $currentUser -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description 'Starts PerformanceOverlay with elevated privileges when this user signs in.' -Force | Out-Null
Start-ScheduledTask -TaskName $taskName
