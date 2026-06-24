@echo off
setlocal
if "%~1"=="" (
  echo Usage: build-dashboard.bat ^<docker-image-tag^>
  exit /b 1
)

set "IMAGE_TAG=%~1"
set "IMAGE_NAME=jchristn77/xeno-dashboard"
docker build -f dashboard/Dockerfile -t %IMAGE_NAME%:%IMAGE_TAG% -t %IMAGE_NAME%:latest dashboard || exit /b 1
docker push %IMAGE_NAME%:%IMAGE_TAG% || exit /b 1
docker push %IMAGE_NAME%:latest || exit /b 1
endlocal
