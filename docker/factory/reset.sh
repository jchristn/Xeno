#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")/.."

echo "This will reset the Xeno Docker deployment to factory defaults."
echo "Docker containers and volumes for this compose project will be removed."
echo "The docker/xeno.json settings file will be overwritten from factory defaults."
printf "Type RESET to continue: "
IFS= read -r CONFIRM
if [ "$CONFIRM" != "RESET" ]; then
  echo "Reset cancelled."
  exit 1
fi

docker compose -f compose.yaml down -v
mkdir -p data logs
cp factory/xeno.json xeno.json
docker compose -f compose.yaml up -d
