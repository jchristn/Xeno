@echo off
REM Build and run the Xeno Agent tray app (xeno-tray) from source. Its menu opens the desktop app, the terminal app,
REM and the local web dashboard, and starts and stops the client.
REM
REM Usage: run-tray.bat [--home <dir>]
setlocal
call "%~dp0env.bat"
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
call "%~dp0build-apps.bat" run-tray
if errorlevel 1 (endlocal & exit /b 1)
echo [run-tray] Starting xeno-tray (look for Xeno Agent in the notification area)...
"%XENO_APP_DIR%\xeno-tray.exe" %*
endlocal & exit /b %ERRORLEVEL%

:usage
echo(Build and run the Xeno Agent tray app (xeno-tray) from source. Its menu opens the desktop app, the terminal app,
echo(and the local web dashboard, and starts and stops the client.
echo.
echo(Usage: run-tray.bat [--home ^<dir^>]
endlocal & exit /b 0
