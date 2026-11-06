#!/usr/bin/env bash
# Sobe ou derruba somente o perfil dev (padrão) ou qa, com volumes próprios.
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"
source scripts/server-profile.sh "${2:-dev}"
case "${1:-up}" in
  up)
    scripts/setup-e2e-database.sh "$prisma_server_profile"
    prisma_server_load_env
    if [[ "$prisma_server_profile" == qa ]]; then
      # QA usa a imagem pinada da main; não reconstrói com a branch de testes.
      docker image inspect "$PRISMA_SERVER_IMAGE" >/dev/null
      docker compose -p "$prisma_server_project" --env-file "$prisma_server_env" -f compose.server-dev.yaml up --no-build --pull never -d --wait
    else
      docker compose -p "$prisma_server_project" --env-file "$prisma_server_env" -f compose.server-dev.yaml up --build -d --wait
    fi
    echo "App e API: http://localhost:$PRISMA_HTTP_PORT; SQL: 127.0.0.1:$PRISMA_SQL_PORT ($prisma_server_project)."
    ;;
  down)
    prisma_server_load_env
    docker compose -p "$prisma_server_project" --env-file "$prisma_server_env" -f compose.server-dev.yaml down
    ;;
  *) echo 'Uso: scripts/run-local.sh [up|down] [dev|qa]' >&2; exit 1 ;;
esac
