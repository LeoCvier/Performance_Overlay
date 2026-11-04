$ErrorActionPreference = 'SilentlyContinue'
Unregister-ScheduledTask -TaskName 'PerformanceOverlay' -Confirm:$false
