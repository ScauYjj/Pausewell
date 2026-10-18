@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Collect-OfficeTrial.ps1"
if errorlevel 1 pause
