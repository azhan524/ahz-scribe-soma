$ErrorActionPreference = 'Stop'
$root = 'D:\max-assistant\models\ollama\no_think\ahz-scribe\tools\audit-broker'
$exe = Join-Path $root 'bin\AhzScribe.AuditBroker.exe'
$audit = Join-Path $env:ProgramData 'AhzScribe\audit'
if (-not (Test-Path $exe)) { throw "missing $exe" }
New-Item -ItemType Directory -Force -Path $audit | Out-Null
Get-Process AuditBroker -ErrorAction SilentlyContinue | Stop-Process -Force
& sc.exe stop AhzAuditBroker *> $null
& sc.exe delete AhzAuditBroker *> $null
Start-Sleep -Seconds 2
& sc.exe create AhzAuditBroker binPath= "`"$exe`"" start= auto obj= LocalSystem DisplayName= "Ahz Scribe Audit Broker" | Out-Null
& sc.exe start AhzAuditBroker | Out-Null
Start-Sleep -Seconds 2
& sc.exe query AhzAuditBroker
