param(
    [switch]$NoBody,
    [switch]$NoFace
)

$ErrorActionPreference = "Stop"
$repo = $PSScriptRoot
$dotnetRoot = Join-Path (Split-Path $repo -Parent) "local-agent\.dotnet"
if (Test-Path (Join-Path $dotnetRoot "dotnet.exe")) {
    $env:DOTNET_ROOT = $dotnetRoot
    $env:PATH = "$dotnetRoot;$env:PATH"
}

$bodyExe = Join-Path $repo "ahz-scribe-body\src\bin\Debug\net10.0-windows\AhzScribe.Body.exe"
$faceExe = Join-Path $repo "ahz-scribe-face\src\bin\Debug\net10.0-windows\AhzScribe.Face.exe"

if (-not (Test-Path $bodyExe)) { throw "body 未构建：$bodyExe" }
if (-not (Test-Path $faceExe)) { throw "face 未构建：$faceExe" }

$env:AHZ_BODY_PIPE = "ahz-scribe-body"
$env:AHZ_BODY_DATA = Join-Path $repo "_body-data"
if ([string]::IsNullOrWhiteSpace($env:AHZ_MODEL_BASE)) { $env:AHZ_MODEL_BASE = "https://api.deepseek.com" }
if ([string]::IsNullOrWhiteSpace($env:AHZ_MODEL_NAME)) { $env:AHZ_MODEL_NAME = "deepseek-v4-pro" }

if ([string]::IsNullOrWhiteSpace($env:DEEPSEEK_API_KEY)) {
    $key = [Environment]::GetEnvironmentVariable("DEEPSEEK_API_KEY", "User")
    if ([string]::IsNullOrWhiteSpace($key)) { $key = [Environment]::GetEnvironmentVariable("DEEPSEEK_API_KEY", "Machine") }
    if (-not [string]::IsNullOrWhiteSpace($key)) { $env:DEEPSEEK_API_KEY = $key }
}

if (-not $NoBody) {
    $body = Get-Process AhzScribe.Body -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited } | Select-Object -First 1
    if ($body) {
        Write-Host "body 已在运行：PID $($body.Id)"
    } else {
        Start-Process -FilePath $bodyExe -WorkingDirectory (Split-Path $bodyExe -Parent) -WindowStyle Hidden
        Start-Sleep -Milliseconds 800
        Write-Host "body 已启动：$bodyExe"
    }
}

if (-not $NoFace) {
    $face = Get-Process AhzScribe.Face -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited } | Select-Object -First 1
    if ($face) {
        Write-Host "face 已在运行：PID $($face.Id)"
    } else {
        Start-Process -FilePath $faceExe -ArgumentList "--ui" -WorkingDirectory (Split-Path $faceExe -Parent)
        Write-Host "face 已启动：$faceExe"
    }
}

if ([string]::IsNullOrWhiteSpace($env:DEEPSEEK_API_KEY)) {
    Write-Host "未检测到 DEEPSEEK_API_KEY，将以 EchoModel 启动；/ask 不会调用真实模型。"
} else {
    Write-Host "模型：$env:AHZ_MODEL_NAME @ $env:AHZ_MODEL_BASE"
}
