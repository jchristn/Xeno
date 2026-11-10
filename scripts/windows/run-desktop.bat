@echo off
REM Build and run the Xeno desktop app (xeno-desktop) from source. It starts the Xeno Agent tray app too, unless
REM XENO_AGENT_AUTOSTART=0.
REM
REM Usage: run-desktop.bat [--server <cloud^|url^|host[:port]>] [--home <dir>] [--page <page>]
REM   run-desktop.bat                                 use the server in client.json (the Xeno cloud by default)
REM   run-desktop.bat --server http://localhost:9000  point this client at a local server and save it
REM   run-desktop.bat --server cloud                  point back at the Xeno cloud
setlocal
call "%~dp0env.bat"
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
call "%~dp0build-apps.bat" run-desktop
if errorlevel 1 (endlocal & exit /b 1)
echo [run-desktop] Starting xeno-desktop...
"%XENO_APP_DIR%\xeno-desktop.exe" %*
endlocal & exit /b %ERRORLEVEL%

:usage
echo(Build and run the Xeno desktop app (xeno-desktop) from source. It starts the Xeno Agent tray app too, unless
echo(XENO_AGENT_AUTOSTART=0.
echo.
echo(Usage: run-desktop.bat [--server ^<cloud^|url^|host[:port]^>] [--home ^<dir^>] [--page ^<page^>]
echo(  run-desktop.bat                                 use the server in client.json (the Xeno cloud by default)
echo(  run-desktop.bat --server http://localhost:9000  point this client at a local server and save it
echo(  run-desktop.bat --server cloud                  point back at the Xeno cloud
endlocal & exit /b 0
