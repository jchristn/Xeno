@echo off
setlocal
pushd dashboard || exit /b 1
npm install || exit /b 1
npm run build:local || exit /b 1
popd
endlocal
