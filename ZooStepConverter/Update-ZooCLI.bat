@echo off
setlocal
echo ========================================================
echo   Zoo CLI Safe Windows Updater (Bypasses OS Error 5)
echo ========================================================
echo.

set "ZOO_DIR=%LOCALAPPDATA%\Zoo"
set "ZOO_EXE=%ZOO_DIR%\zoo.exe"
set "ZOO_TMP=%ZOO_DIR%\zoo.new.exe"
set "ZOO_BAK=%ZOO_DIR%\zoo.old.exe"
set "DOWNLOAD_URL=https://github.com/KittyCAD/cli/releases/download/v0.2.204/zoo-x86_64-pc-windows-gnu"

if not exist "%ZOO_DIR%" mkdir "%ZOO_DIR%"

echo [1/3] Closing any running zoo processes...
taskkill /F /IM zoo.exe >nul 2>&1

echo [2/3] Downloading latest Zoo CLI binary...
if exist "%ZOO_TMP%" del /f /q "%ZOO_TMP%" >nul 2>&1

where curl.exe >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    curl.exe -L -o "%ZOO_TMP%" "%DOWNLOAD_URL%"
) else (
    powershell -NoProfile -Command ^
      "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; " ^
      "(New-Object System.Net.WebClient).DownloadFile('%DOWNLOAD_URL%', '%ZOO_TMP%')"
)

if not exist "%ZOO_TMP%" (
    echo [ERROR] Download failed. Check your internet connection.
    pause
    exit /b 1
)

echo [3/3] Installing binary...
if exist "%ZOO_BAK%" del /f /q "%ZOO_BAK%" >nul 2>&1
if exist "%ZOO_EXE%" ren "%ZOO_EXE%" zoo.old.exe
move /y "%ZOO_TMP%" "%ZOO_EXE%" >nul
if exist "%ZOO_BAK%" del /f /q "%ZOO_BAK%" >nul 2>&1

echo.
echo ========================================================
echo   SUCCESS! Zoo CLI is updated.
echo ========================================================
"%ZOO_EXE%" version
echo.
pause
