@echo off
REM Run the Xeno server web dashboard (dashboard\, Vite dev server) at http://localhost:9100. Use it to sign in to a
REM server and manage tenants, users, access keys, clients, and requests.
REM
REM Usage: run-dashboard.bat
setlocal
call "%~dp0env.bat"
where npm >nul 2>nul || (
    echo [run-dashboard] npm is required ^(install Node.js 20 or later^).
    exit /b 1
)
pushd "%XENO_REPO_ROOT%\dashboard"
if not exist node_modules (
    echo [run-dashboard] Installing dashboard packages...
    call npm.cmd install || (popd & exit /b 1)
)
echo [run-dashboard] Starting the dashboard at http://localhost:9100 (Ctrl+C to stop)...
call npm.cmd run dev
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
