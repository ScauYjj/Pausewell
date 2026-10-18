@echo off
setlocal
cd /d "%~dp0"
if exist "artifacts\Pausewell-win-x64\Pausewell.exe" (
  start "" "artifacts\Pausewell-win-x64\Pausewell.exe"
  exit /b 0
)
echo Packaged executable not found. Run scripts\build.ps1 first.
pause
exit /b 1
