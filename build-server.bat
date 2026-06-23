@echo off
setlocal
docker build -f src/Xeno.Server/Dockerfile -t jchristn77/xeno-server:v0.1.0 -t jchristn77/xeno-server:latest .
endlocal
