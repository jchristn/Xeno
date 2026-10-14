@echo off
REM Remove and then freshly install Xeno from this checkout (the xeno tool, the desktop app, and the tray agent).
REM Your client configuration in %USERPROFILE%\.xeno is kept. Arguments are passed to install.bat.
REM
REM Usage: reinstall.bat [--no-apps] [--no-dashboard]
setlocal
echo [reinstall] Removing the current installation...
call "%~dp0remove.bat" || exit /b 1
echo.
echo [reinstall] Installing...
call "%~dp0install.bat" %*
exit /b %ERRORLEVEL%
