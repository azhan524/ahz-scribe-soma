@echo off
chcp 65001 >nul
setlocal
set "DIR=%~dp0"
set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo CSC not found
  exit /b 1
)
if not exist "%DIR%bin" mkdir "%DIR%bin"
"%CSC%" /nologo /target:exe /out:"%DIR%bin\AhzScribe.AuditBroker.exe" /r:System.dll /r:System.Core.dll /r:System.ServiceProcess.dll /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.IO.Pipes.dll "%DIR%AhzScribe.AuditBroker.cs"
if errorlevel 1 (
  echo BROKER_BUILD: FAILED
  exit /b 1
)
echo BROKER_BUILD: OK
