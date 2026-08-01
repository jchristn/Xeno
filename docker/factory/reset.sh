#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")/.."

echo "This will reset the Xeno Docker deployment to factory defaults."
echo "Docker containers, volumes, local runtime data, and logs for this compose project will be removed."
echo "This includes NetLedger archive catalog data and all Less3 object/database/log data."
echo "The following settings files will be overwritten from factory defaults:"
echo "  docker/xeno.json"
echo "  docker/netledger/netledger.json"
echo "  docker/netledger-archive/netledger.json"
echo "  docker/less3/system.json"
printf "Type RESET to continue: "
IFS= read -r CONFIRM
if [ "$CONFIRM" != "RESET" ]; then
  echo "Reset cancelled."
  exit 1
fi

# Telemetry backends (Prometheus, Grafana, Tempo, Loki) keep no named volumes,
# so their data is discarded with the containers here; nothing extra to purge.
docker compose -f compose.yaml down --volumes --remove-orphans

rm -rf data logs netledger netledger-archive
mkdir -p logs netledger netledger-archive

# Recreate Less3 runtime directories and purge object/log/temp data.
# Less3 control-plane metadata lives in the "less3" PostgreSQL database, which
# is dropped with the compose volumes above and recreated empty by init.sql.
mkdir -p less3/disk less3/logs less3/temp
find less3/disk -mindepth 1 ! -name .gitkeep -exec rm -rf {} +
find less3/temp -mindepth 1 ! -name .gitkeep -exec rm -rf {} +
find less3/logs -mindepth 1 ! -name .gitkeep -exec rm -rf {} +

cp factory/xeno.json xeno.json
cp factory/netledger.json netledger/netledger.json
cp factory/netledger.archive.json netledger-archive/netledger.json
cp factory/less3.system.json less3/system.json

docker compose -f compose.yaml pull
docker compose -f compose.yaml up -d
