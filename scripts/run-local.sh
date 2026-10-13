#!/usr/bin/env bash
# Sobe ou derruba somente o projeto Docker prisma-dev.
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"
case "${1:-up}" in
  up)
    scripts/setup-e2e-database.sh
    docker compose -p prisma-dev --env-file .env.server-dev -f compose.server-dev.yaml up --build -d --wait
    echo 'App e API: http://localhost:3100; SQL: 127.0.0.1:3102.'
    ;;
  down)
    docker compose -p prisma-dev --env-file .env.server-dev -f compose.server-dev.yaml down
    ;;
  *) echo 'Uso: scripts/run-local.sh [up|down]' >&2; exit 1 ;;
esac
