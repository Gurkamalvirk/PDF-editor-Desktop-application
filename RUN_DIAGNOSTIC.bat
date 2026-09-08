@echo off
setlocal
cd /d "%~dp0"
set "EXE=%~dp0publish\win-x64\PdfTextEditor.exe"
set "LOG=%LOCALAPPDATA%\PdfTextEditor\startup-error.log"

echo PDF Text Editor diagnostic launcher
echo =================================
echo.

if not exist "%EXE%" (
  echo ERROR: %EXE% does not exist.
  echo Run BUILD_ON_WINDOWS.bat first.
  pause
  exit /b 1
)

echo Executable: %EXE%
if exist "%~dp0publish\win-x64\pdfium.dll" (
  echo pdfium.dll: FOUND
) else (
  echo pdfium.dll: NOT FOUND IN EXE FOLDER
  echo Searching publish folder...
  dir /s /b "%~dp0publish\win-x64\pdfium.dll" 2^>nul
)
echo.
echo Starting application and waiting for it to exit...
start "" /wait "%EXE%"
set "CODE=%ERRORLEVEL%"

echo.
echo Process exit code: %CODE%
echo Diagnostic log: %LOG%
echo.
if exist "%LOG%" (
  echo ---------------- LOG ----------------
  type "%LOG%"
  echo -------------- END LOG --------------
) else (
  echo No startup-error.log exists yet.
)
echo.
pause
exit /b %CODE%
