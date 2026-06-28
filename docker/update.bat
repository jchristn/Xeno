@echo off
setlocal

pushd "%~dp0" || exit /b 1
docker compose pull xeno-postgres && docker compose down && docker compose up -d --build && docker ps -a
set "EXITCODE=%ERRORLEVEL%"
popd
exit /b %EXITCODE%
