@echo off
setlocal
if "%~1"=="" (
  echo Usage: build-server.bat ^<docker-image-tag^>
  exit /b 1
)

set "IMAGE_TAG=%~1"
set "IMAGE_NAME=jchristn77/xeno-server"
docker build -f src/Xeno.Server/Dockerfile -t %IMAGE_NAME%:%IMAGE_TAG% -t %IMAGE_NAME%:latest . || exit /b 1
docker push %IMAGE_NAME%:%IMAGE_TAG% || exit /b 1
docker push %IMAGE_NAME%:latest || exit /b 1
endlocal
