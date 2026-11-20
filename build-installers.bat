@echo off
REM Build every Xeno installer Windows can produce into installers\<version>\:
REM   the setup .exe (win-x64, win-arm64) and the portable .zip archives, plus SHA256SUMS.
REM Run build-installers.sh on macOS and Linux for theirs. All three operating systems build together in CI:
REM push a v<version> tag (see .github\workflows\release.yml). Needs Inno Setup 6: choco install innosetup
REM
REM Usage: build-installers.bat [version] [--clean]
setlocal enabledelayedexpansion
cd /d "%~dp0"

set "VERSION_ARGS="
set "CLEAN="
:args
if "%~1"=="" goto :build
if /i "%~1"=="--clean" (set "CLEAN=--clean") else if /i "%~1"=="-h" (goto :usage) else if /i "%~1"=="--help" (goto :usage) else (set "VERSION_ARGS=--version %~1")
shift
goto :args

:build
echo Building the Xeno publisher...
dotnet build src\Xeno.Publisher\Xeno.Publisher.csproj -c Release --nologo -v q
if errorlevel 1 exit /b 1
set "PUB=src\Xeno.Publisher\bin\Release\net10.0\xeno-publish.dll"

for /f "delims=" %%v in ('dotnet "%PUB%" --print-version %VERSION_ARGS%') do set "VERSION=%%v"
set "OUT=installers\%VERSION%"
echo.
echo Version : %VERSION%
echo Output  : %OUT%

dotnet "%PUB%" --channel installers --sums --output "%OUT%" %VERSION_ARGS% %CLEAN%
if errorlevel 1 exit /b 1

echo.
echo Installers are in %OUT%. Install one, open a new terminal, and run: xeno --version
endlocal & exit /b 0

:usage
echo Usage: build-installers.bat [version] [--clean]
echo   version   override the version (default: ^<Version^> in src\Directory.Build.props)
echo   --clean   rebuild the local web dashboard even if dashboard\dist-local exists
endlocal & exit /b 0
