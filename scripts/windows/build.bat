@echo off
REM Build the whole Xeno solution (server, client, desktop app, tray agent, terminal app, and tests).
REM
REM Usage: build.bat [extra dotnet build arguments...]
setlocal
call "%~dp0env.bat"
where dotnet >nul 2>nul
if errorlevel 1 goto :no_dotnet
echo [build] Building src\Xeno.sln (%XENO_CONFIGURATION%)...
dotnet build "%XENO_REPO_ROOT%\src\Xeno.sln" -c %XENO_CONFIGURATION% %*
endlocal & exit /b %ERRORLEVEL%

:no_dotnet
echo ERROR: 'dotnet' was not found on PATH. Install the .NET 10 SDK: https://dotnet.microsoft.com/download 1>&2
endlocal & exit /b 1
