@echo off
REM Build the whole Xeno solution (server, client, desktop app, tray agent, terminal app, and tests).
REM
REM Usage: build.bat [extra dotnet build arguments...]
setlocal
call "%~dp0env.bat"
echo [build] Building src\Xeno.sln (%XENO_CONFIGURATION%)...
dotnet build "%XENO_REPO_ROOT%\src\Xeno.sln" -c %XENO_CONFIGURATION% %*
exit /b %ERRORLEVEL%
