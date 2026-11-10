@echo off
REM Build and run the Xeno terminal app (xeno tui) from source in this console.
REM
REM Usage: run-tui.bat [--server <cloud^|url^|host[:port]>] [--home <dir>] [--page <page>]
REM   run-tui.bat                                 use the server in client.json (the Xeno cloud by default)
REM   run-tui.bat --server http://localhost:9000  point this client at a local server and save it
REM   run-tui.bat --server cloud                  point back at the Xeno cloud
REM Keys: F10 menu, Ctrl+K command palette, F1 help, Alt+1..9 pages, Ctrl+Q quit.
setlocal
call "%~dp0env.bat"
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
call "%~dp0build-apps.bat" run-tui
if errorlevel 1 (endlocal & exit /b 1)
echo [run-tui] Starting xeno tui...
"%XENO_APP_DIR%\xeno.exe" tui %*
endlocal & exit /b %ERRORLEVEL%

:usage
echo(Build and run the Xeno terminal app (xeno tui) from source in this console.
echo.
echo(Usage: run-tui.bat [--server ^<cloud^|url^|host[:port]^>] [--home ^<dir^>] [--page ^<page^>]
echo(  run-tui.bat                                 use the server in client.json (the Xeno cloud by default)
echo(  run-tui.bat --server http://localhost:9000  point this client at a local server and save it
echo(  run-tui.bat --server cloud                  point back at the Xeno cloud
echo(Keys: F10 menu, Ctrl+K command palette, F1 help, Alt+1..9 pages, Ctrl+Q quit.
endlocal & exit /b 0
