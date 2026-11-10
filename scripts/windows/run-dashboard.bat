@echo off
REM Run the Xeno server web dashboard (dashboard\, Vite dev server) at http://localhost:9100. Use it to sign in to a
REM server and manage tenants, users, access keys, clients, and requests.
REM
REM Usage: run-dashboard.bat
setlocal
call "%~dp0env.bat"
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
if not "%~1"=="" goto :bad_arg
where npm >nul 2>nul
if errorlevel 1 goto :no_npm
pushd "%XENO_REPO_ROOT%\dashboard"
if exist node_modules goto :dev
echo [run-dashboard] Installing dashboard packages...
call npm install
if errorlevel 1 (
    popd
    endlocal & exit /b 1
)
:dev
echo [run-dashboard] Starting the dashboard at http://localhost:9100 (Ctrl+C to stop)...
call npm run dev
set "EXIT_CODE=%ERRORLEVEL%"
popd
endlocal & exit /b %EXIT_CODE%

:no_npm
echo [run-dashboard] npm is required ^(install Node.js 20 or later^). 1>&2
endlocal & exit /b 1

:bad_arg
echo [run-dashboard] Unknown argument: %1 ^(this script takes no arguments^) 1>&2
endlocal & exit /b 2

:usage
echo(Run the Xeno server web dashboard (Vite dev server) in the foreground at http://localhost:9100.
echo.
echo(Usage: run-dashboard.bat
endlocal & exit /b 0
