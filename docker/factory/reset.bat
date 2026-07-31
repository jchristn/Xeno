@echo off
setlocal enabledelayedexpansion
pushd "%~dp0\.."

echo This will reset the Xeno Docker deployment to factory defaults.
echo Docker containers, volumes, local runtime data, and logs for this compose project will be removed.
echo This includes NetLedger archive catalog data and all Less3 object/database/log data.
echo The following settings files will be overwritten from factory defaults:
echo   docker\xeno.json
echo   docker\netledger\netledger.json
echo   docker\netledger-archive\netledger.json
echo   docker\less3\system.json
set "CONFIRM="
set /P "CONFIRM=Type RESET to continue: "
if not "%CONFIRM%"=="RESET" (
  echo Reset cancelled.
  popd
  endlocal
  exit /b 1
)

docker compose -f compose.yaml down --volumes --remove-orphans

if exist data rmdir /S /Q data
if exist logs rmdir /S /Q logs
if exist netledger rmdir /S /Q netledger
if exist netledger-archive rmdir /S /Q netledger-archive
if not exist logs mkdir logs
if not exist netledger mkdir netledger
if not exist netledger-archive mkdir netledger-archive

REM Recreate Less3 runtime directories and purge object/log/temp data.
REM Less3 control-plane metadata lives in the "less3" PostgreSQL database, which
REM is dropped with the compose volumes above and recreated empty by init.sql.
if not exist less3 mkdir less3
if not exist less3\disk mkdir less3\disk
if not exist less3\logs mkdir less3\logs
if not exist less3\temp mkdir less3\temp
for %%D in (disk temp logs) do (
  for %%F in (less3\%%D\*) do (
    if /I not "%%~nxF"==".gitkeep" del /F /Q "%%F" 2>NUL
  )
  for /D %%G in (less3\%%D\*) do rmdir /S /Q "%%G" 2>NUL
)

copy /Y factory\xeno.json xeno.json >NUL
copy /Y factory\netledger.json netledger\netledger.json >NUL
copy /Y factory\netledger.archive.json netledger-archive\netledger.json >NUL
copy /Y factory\less3.system.json less3\system.json >NUL

docker compose -f compose.yaml pull
docker compose -f compose.yaml up -d
popd
endlocal
