#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")/.."
docker compose -f compose.yaml down -v
mkdir -p data logs
cp factory/xeno.json xeno.json
docker compose -f compose.yaml up -d
