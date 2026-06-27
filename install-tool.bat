@echo off
setlocal

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\Xeno.Client\Xeno.Client.csproj"
set "PACKAGE_DIR=%ROOT%artifacts\tools"

echo Removing existing xeno global tool installation, if present...
dotnet tool uninstall --global xeno >nul 2>nul
dotnet tool uninstall --global Xeno.Client >nul 2>nul

if not exist "%PACKAGE_DIR%" mkdir "%PACKAGE_DIR%"
del /q "%PACKAGE_DIR%\xeno*.nupkg" >nul 2>nul
del /q "%PACKAGE_DIR%\Xeno.Client*.nupkg" >nul 2>nul

echo Building xeno from source...
pushd "%ROOT%dashboard" || exit /b 1
call npm.cmd install || exit /b 1
call npm.cmd run build:local || exit /b 1
popd
dotnet restore "%PROJECT%" || exit /b 1
dotnet build "%PROJECT%" -c Release || exit /b 1

echo Packing xeno global tool...
dotnet pack "%PROJECT%" -c Release --no-build -o "%PACKAGE_DIR%" || exit /b 1

echo Installing xeno as a global tool...
dotnet tool install --global xeno --add-source "%PACKAGE_DIR%" || exit /b 1

echo.
echo xeno installed successfully.
echo Run "xeno help" to verify the tool is available on your PATH.

endlocal
