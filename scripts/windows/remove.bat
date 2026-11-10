@echo off
REM Remove the Xeno installation: stops the client worker, quits the desktop app and the tray agent, uninstalls the
REM xeno global tool, and deletes %LOCALAPPDATA%\Programs\Xeno and the Start menu shortcuts. Succeeds when nothing is
REM installed. Your client configuration and history in %USERPROFILE%\.xeno are kept; use factory-reset.bat to delete
REM them.
REM
REM Usage: remove.bat
setlocal
call "%~dp0env.bat"
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
if not "%~1"=="" goto :bad_arg

if not defined XENO_CLI goto :quit_apps
echo [remove] Stopping the client worker if it is running...
"%XENO_CLI%" stop >nul 2>nul
:quit_apps
echo [remove] Quitting the desktop app and tray agent if they are running...
taskkill /IM xeno-desktop.exe /F >nul 2>nul
taskkill /IM xeno-tray.exe /F >nul 2>nul

echo [remove] Uninstalling the xeno global tool if present...
where dotnet >nul 2>nul
if errorlevel 1 goto :no_dotnet
call :tool_installed
if errorlevel 1 goto :not_installed
dotnet tool uninstall --global xeno >nul 2>nul
dotnet tool uninstall --global Xeno.Client >nul 2>nul
call :tool_installed
if errorlevel 1 goto :apps
echo [remove] ERROR: could not uninstall the xeno global tool. Close every xeno process ^(xeno tui, xeno connect^) and try again. 1>&2
endlocal & exit /b 1
:not_installed
echo [remove] The xeno global tool is not installed; nothing to uninstall.

:apps
if not exist "%XENO_INSTALL_DIR%" goto :shortcuts
echo [remove] Removing %XENO_INSTALL_DIR%
rmdir /s /q "%XENO_INSTALL_DIR%"
if exist "%XENO_INSTALL_DIR%" goto :apps_locked
:shortcuts
del /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Xeno.lnk" >nul 2>nul
del /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Xeno Agent.lnk" >nul 2>nul

echo [remove] Completed. Client data in %XENO_HOME_DIR% was kept.
endlocal & exit /b 0

:apps_locked
echo [remove] ERROR: could not delete %XENO_INSTALL_DIR%. Close Xeno and Xeno Agent and try again. 1>&2
endlocal & exit /b 1

REM Exit code 0 when the xeno tool (or the older Xeno.Client package) is installed.
:tool_installed
dotnet tool list --global 2>nul | findstr /r /i /b /c:"xeno " /c:"xeno\.client " >nul
exit /b %ERRORLEVEL%

:no_dotnet
echo [remove] WARNING: dotnet was not found, so the xeno tool uninstall was skipped. Install the .NET SDK and run remove again to uninstall it. 1>&2
goto :apps

:bad_arg
echo [remove] Unknown argument: %1 ^(this script takes no arguments^) 1>&2
endlocal & exit /b 2

:usage
echo(Remove the Xeno installation (the xeno tool, the desktop app, the tray agent, and the Start menu shortcuts).
echo(Your client configuration and history are kept; use factory-reset.bat to delete them.
echo.
echo(Usage: remove.bat
endlocal & exit /b 0
