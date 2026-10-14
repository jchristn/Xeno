@echo off
REM Build and run the Xeno Agent tray app (xeno-tray) from source. Its menu opens the desktop app, the terminal app,
REM and the local web dashboard, and starts and stops the client.
REM
REM Usage: run-tray.bat [--home <dir>]
setlocal
call "%~dp0env.bat"
call "%~dp0build-apps.bat" run-tray || exit /b 1
echo [run-tray] Starting xeno-tray (look for Xeno Agent in the notification area)...
"%XENO_APP_DIR%\xeno-tray.exe" %*
exit /b %ERRORLEVEL%
