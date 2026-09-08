@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-release.ps1"
if errorlevel 1 (
  echo.
  echo Build failed. Make sure .NET 8 SDK or Visual Studio 2022 .NET desktop development is installed.
  pause
  exit /b 1
)
echo.
echo Build complete. Open publish\win-x64\PdfTextEditor.exe
pause
