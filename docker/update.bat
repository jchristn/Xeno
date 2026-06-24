@echo off
setlocal

pushd "%~dp0" || exit /b 1
docker compose pull && docker compose down && docker compose up -d && docker ps -a
set "EXITCODE=%ERRORLEVEL%"
popd
exit /b %EXITCODE%
