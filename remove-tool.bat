@echo off
setlocal

set "REMOVED=0"

echo Removing xeno global tool...
dotnet tool uninstall --global xeno >nul 2>nul
if "%ERRORLEVEL%"=="0" set "REMOVED=1"

dotnet tool uninstall --global Xeno.Client >nul 2>nul
if "%ERRORLEVEL%"=="0" set "REMOVED=1"

if "%REMOVED%"=="0" (
  echo xeno global tool was not installed.
) else (
  echo xeno global tool removed.
)

endlocal
