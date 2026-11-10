@echo off
REM Install Xeno from this checkout:
REM   1. builds the client's local web dashboard (dashboard\dist-local) so it is embedded in xeno,
REM   2. packs src\Xeno.Client and installs it as the global .NET tool "xeno" (which includes "xeno tui"),
REM   3. publishes the desktop app and the tray agent to %LOCALAPPDATA%\Programs\Xeno and adds Start menu shortcuts
REM      for "Xeno" and "Xeno Agent".
REM It stops the client worker and the apps first, so their files can be replaced.
REM Your client configuration in %USERPROFILE%\.xeno is kept. New installs point at the Xeno cloud server.
REM
REM Usage: install.bat [--no-apps] [--no-dashboard]
setlocal
call "%~dp0env.bat"
set "INSTALL_APPS=1"
set "BUILD_DASHBOARD=1"
:parse
if "%~1"=="" goto :parsed
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
if /i "%~1"=="--no-apps" (
    set "INSTALL_APPS=0"
    shift /1
    goto :parse
)
if /i "%~1"=="--no-dashboard" (
    set "BUILD_DASHBOARD=0"
    shift /1
    goto :parse
)
goto :bad_arg

:parsed
where dotnet >nul 2>nul
if errorlevel 1 goto :no_dotnet

REM A running worker or app keeps its files locked; stop them before replacing the tool and the apps.
if not defined XENO_CLI goto :stop_apps
echo [install] Stopping the client worker if it is running...
"%XENO_CLI%" stop >nul 2>nul
:stop_apps
taskkill /IM xeno-desktop.exe /F >nul 2>nul
taskkill /IM xeno-tray.exe /F >nul 2>nul

if "%BUILD_DASHBOARD%"=="0" goto :pack
where npm >nul 2>nul
if errorlevel 1 (
    echo [install] WARNING: npm not found; the local web dashboard will not be embedded in xeno. 1>&2
    goto :pack
)
echo.
echo [install] Building the local web dashboard...
pushd "%XENO_REPO_ROOT%\dashboard"
call npm install --silent
if errorlevel 1 goto :npm_failed
call npm run build:local
if errorlevel 1 goto :npm_failed
popd

:pack
echo.
echo [install] Packing the xeno tool (Release)...
if not exist "%XENO_TOOL_PACKAGES%" mkdir "%XENO_TOOL_PACKAGES%"
del /q "%XENO_TOOL_PACKAGES%\xeno*.nupkg" >nul 2>nul
del /q "%XENO_TOOL_PACKAGES%\Xeno.Client*.nupkg" >nul 2>nul
dotnet pack "%XENO_CLIENT_PROJECT%" -c Release --nologo -v quiet -o "%XENO_TOOL_PACKAGES%"
if errorlevel 1 goto :failed

echo.
echo [install] Installing the xeno global tool...
dotnet tool uninstall --global xeno >nul 2>nul
dotnet tool uninstall --global Xeno.Client >nul 2>nul
dotnet tool list --global 2>nul | findstr /r /i /b /c:"xeno " /c:"xeno\.client " >nul
if not errorlevel 1 goto :tool_locked
dotnet tool install --global xeno --add-source "%XENO_TOOL_PACKAGES%"
if errorlevel 1 goto :failed

if "%INSTALL_APPS%"=="0" goto :done
set "RID=win-x64"
if /i "%PROCESSOR_ARCHITECTURE%"=="ARM64" set "RID=win-arm64"
echo.
echo [install-apps] Publishing xeno-desktop and xeno-tray (%RID%) to %XENO_INSTALL_DIR%...
if exist "%XENO_INSTALL_DIR%" rmdir /s /q "%XENO_INSTALL_DIR%"
if exist "%XENO_INSTALL_DIR%" goto :apps_locked
dotnet publish "%XENO_DESKTOP_PROJECT%" -c Release -r %RID% --self-contained false --nologo -v quiet -o "%XENO_INSTALL_DIR%"
if errorlevel 1 goto :failed

echo [install-apps] Adding Start menu shortcuts...
powershell -NoProfile -Command "$s = New-Object -ComObject WScript.Shell; $dir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'; foreach ($pair in @(@('Xeno','xeno-desktop.exe'), @('Xeno Agent','xeno-tray.exe'))) { $l = $s.CreateShortcut((Join-Path $dir ($pair[0] + '.lnk'))); $l.TargetPath = Join-Path $env:XENO_INSTALL_DIR $pair[1]; $l.WorkingDirectory = $env:XENO_INSTALL_DIR; $l.Save() }"
if errorlevel 1 goto :failed
echo [install-apps] Installed. Start the desktop app from the Start menu (Xeno).

:done
echo.
echo [install] Completed.
where xeno.exe >nul 2>nul
if errorlevel 1 echo [install] Add %USERPROFILE%\.dotnet\tools to your PATH to run xeno and xeno tui ^(a new terminal usually has it^).
echo [install] Try: xeno status ^| xeno tui ^| xeno server show
endlocal & exit /b 0

:tool_locked
echo [install] ERROR: could not uninstall the xeno global tool. Close every xeno process ^(xeno tui, xeno connect^) and try again. 1>&2
endlocal & exit /b 1

:npm_failed
popd
:failed
echo [install] ERROR: the install failed; see the messages above. 1>&2
endlocal & exit /b 1

:apps_locked
echo [install] ERROR: could not replace %XENO_INSTALL_DIR%. Close Xeno and Xeno Agent and try again. 1>&2
endlocal & exit /b 1

:bad_arg
echo [install] Unknown argument: %1 1>&2
endlocal & exit /b 2

:no_dotnet
echo ERROR: 'dotnet' was not found on PATH. Install the .NET 10 SDK: https://dotnet.microsoft.com/download 1>&2
endlocal & exit /b 1

:usage
echo(Install Xeno from this checkout: the xeno global tool, the desktop app, and the tray agent
echo((%LOCALAPPDATA%\Programs\Xeno plus Start menu shortcuts). Your client configuration is kept.
echo.
echo(Usage: install.bat [--no-apps] [--no-dashboard]
echo(  --no-apps        install only the xeno tool
echo(  --no-dashboard   skip the local dashboard build (keeps whatever dashboard\dist-local holds)
endlocal & exit /b 0
