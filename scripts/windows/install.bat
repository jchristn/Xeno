@echo off
REM Install Xeno from this checkout:
REM   1. builds the client's local web dashboard (dashboard\dist-local) so it is embedded in xeno,
REM   2. packs src\Xeno.Client and installs it as the global .NET tool "xeno" (which includes "xeno tui"),
REM   3. publishes the desktop app and the tray agent to %LOCALAPPDATA%\Programs\Xeno and adds Start menu shortcuts
REM      for "Xeno" and "Xeno Agent".
REM Your client configuration in %USERPROFILE%\.xeno is kept. New installs point at the Xeno cloud server.
REM
REM Usage: install.bat [--no-apps] [--no-dashboard]
setlocal
call "%~dp0env.bat"
set "INSTALL_APPS=1"
set "BUILD_DASHBOARD=1"
for %%A in (%*) do (
    if /i "%%~A"=="--no-apps" set "INSTALL_APPS=0"
    if /i "%%~A"=="--no-dashboard" set "BUILD_DASHBOARD=0"
)

if "%BUILD_DASHBOARD%"=="1" (
    where npm >nul 2>nul
    if errorlevel 1 (
        echo [install] WARNING: npm not found; the local web dashboard will not be embedded in xeno.
    ) else (
        echo.
        echo [install] Building the local web dashboard...
        pushd "%XENO_REPO_ROOT%\dashboard"
        call npm.cmd install --silent || (popd & exit /b 1)
        call npm.cmd run build:local || (popd & exit /b 1)
        popd
    )
)

echo.
echo [install] Packing the xeno tool (Release)...
if not exist "%XENO_TOOL_PACKAGES%" mkdir "%XENO_TOOL_PACKAGES%"
del /q "%XENO_TOOL_PACKAGES%\xeno*.nupkg" >nul 2>nul
del /q "%XENO_TOOL_PACKAGES%\Xeno.Client*.nupkg" >nul 2>nul
dotnet pack "%XENO_CLIENT_PROJECT%" -c Release --nologo -v quiet -o "%XENO_TOOL_PACKAGES%"
if errorlevel 1 exit /b 1

echo.
echo [install] Installing the xeno global tool...
dotnet tool uninstall --global xeno >nul 2>nul
dotnet tool uninstall --global Xeno.Client >nul 2>nul
dotnet tool install --global xeno --add-source "%XENO_TOOL_PACKAGES%"
if errorlevel 1 exit /b 1

if "%INSTALL_APPS%"=="0" goto :done
set "RID=win-x64"
if /i "%PROCESSOR_ARCHITECTURE%"=="ARM64" set "RID=win-arm64"
echo.
echo [install] Publishing xeno-desktop and xeno-tray (%RID%) to %XENO_INSTALL_DIR%...
taskkill /IM xeno-desktop.exe /F >nul 2>nul
taskkill /IM xeno-tray.exe /F >nul 2>nul
if exist "%XENO_INSTALL_DIR%" rmdir /s /q "%XENO_INSTALL_DIR%"
dotnet publish "%XENO_DESKTOP_PROJECT%" -c Release -r %RID% --self-contained false --nologo -v quiet -o "%XENO_INSTALL_DIR%"
if errorlevel 1 exit /b 1

echo [install] Adding Start menu shortcuts...
powershell -NoProfile -Command "$s = New-Object -ComObject WScript.Shell; $dir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'; foreach ($pair in @(@('Xeno','xeno-desktop.exe'), @('Xeno Agent','xeno-tray.exe'))) { $l = $s.CreateShortcut((Join-Path $dir ($pair[0] + '.lnk'))); $l.TargetPath = Join-Path $env:XENO_INSTALL_DIR $pair[1]; $l.WorkingDirectory = $env:XENO_INSTALL_DIR; $l.Save() }"

:done
echo.
echo [install] Completed. Try: xeno status ^| xeno tui ^| xeno server show
exit /b 0
