@echo off
REM Remove the Xeno installation: stops the client worker, quits the desktop app and the tray agent, uninstalls the
REM xeno global tool, and deletes %LOCALAPPDATA%\Programs\Xeno and the Start menu shortcuts. Your client configuration
REM and history in %USERPROFILE%\.xeno are kept; use factory-reset.bat to delete them.
REM
REM Usage: remove.bat
setlocal
call "%~dp0env.bat"

where xeno >nul 2>nul && (
    echo [remove] Stopping the client worker if it is running...
    call xeno stop >nul 2>nul
)

echo [remove] Quitting the desktop app and tray agent if they are running...
taskkill /IM xeno-desktop.exe /F >nul 2>nul
taskkill /IM xeno-tray.exe /F >nul 2>nul

echo [remove] Uninstalling the xeno global tool if present...
dotnet tool uninstall --global xeno >nul 2>nul
dotnet tool uninstall --global Xeno.Client >nul 2>nul

if exist "%XENO_INSTALL_DIR%" (
    echo [remove] Removing %XENO_INSTALL_DIR%
    rmdir /s /q "%XENO_INSTALL_DIR%"
)
del /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Xeno.lnk" >nul 2>nul
del /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Xeno Agent.lnk" >nul 2>nul

echo [remove] Completed. Client data in %USERPROFILE%\.xeno was kept.
exit /b 0
