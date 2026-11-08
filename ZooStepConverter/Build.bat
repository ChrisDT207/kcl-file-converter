@echo off
setlocal
echo ========================================================
echo   Building ZooStepConverter (Multi-tier Architecture)
echo ========================================================
echo.

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo [ERROR] C# compiler csc.exe not found under %WINDIR%\Microsoft.NET\Framework[64]\v4.0.30319
    pause
    exit /b 1
)

pushd "%~dp0"
"%CSC%" /nologo /target:winexe /r:System.Security.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /out:ZooStepConverter.exe /recurse:*.cs
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo   SUCCESS: ZooStepConverter.exe built successfully!
    echo ========================================================
) else (
    echo.
    echo [ERROR] Build failed with exit code %ERRORLEVEL%.
)
popd

