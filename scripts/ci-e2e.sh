#!/usr/bin/env bash
# Testa a imagem publicável contra SQL Server exclusivo e descartável.
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
image=${1:?Informe a imagem candidata}
suffix=${GITHUB_RUN_ID:-local-$$}-${GITHUB_RUN_ATTEMPT:-1}
network=prisma-ci-e2e-$suffix
database=prisma-ci-sql-$suffix
app=prisma-ci-app-$suffix
cleanup() {
  docker rm -f "$app" "$database" >/dev/null 2>&1 || true
  docker network rm "$network" >/dev/null 2>&1 || true
}
trap cleanup EXIT
export MSSQL_SA_PASSWORD="E2e#$(openssl rand -hex 20)"
export E2E_TEST_USER_PASSWORD="E2e#$(openssl rand -hex 20)"
export Jwt__Key=$(openssl rand -hex 32)
if [[ ${GITHUB_ACTIONS:-false} == true ]]; then
  printf '::add-mask::%s\n' "$MSSQL_SA_PASSWORD" "$E2E_TEST_USER_PASSWORD" "$Jwt__Key"
fi
docker network create "$network" >/dev/null
docker run -d --name "$database" --network "$network" \
  -e ACCEPT_EULA=Y -e MSSQL_PID=Developer -e MSSQL_SA_PASSWORD \
  mcr.microsoft.com/mssql/server:2022-latest >/dev/null
ready=false
for attempt in $(seq 1 90); do
  if docker exec -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" "$database" \
      /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -Q 'SELECT 1' -b -o /dev/null >/dev/null 2>&1; then
    ready=true; break
  fi
  sleep 2
done
[[ $ready == true ]] || { echo 'SQL Server E2E não ficou pronto.' >&2; exit 1; }
export ConnectionStrings__DefaultConnection="Server=$database,1433;Database=DetranKanban_E2E;User Id=sa;Password=$MSSQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True"
export Seed__DemoPassword=$E2E_TEST_USER_PASSWORD
docker run -d --name "$app" --network "$network" \
  -e ConnectionStrings__DefaultConnection -e Jwt__Key -e Seed__DemoPassword \
  -e ASPNETCORE_ENVIRONMENT=Development -e Seed__DemoEnabled=true -e Setup__Enabled=false \
  -e RateLimiting__GlobalPermitLimit=10000 -e Serilog__MinimumLevel__Default=Warning \
  -e Serilog__MinimumLevel__Override__Microsoft=Warning \
  -e Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command=Warning \
  "$image" >/dev/null
ready=false
for attempt in $(seq 1 90); do
  if docker exec "$app" curl --fail --silent http://localhost:8080/health >/dev/null 2>&1; then
    ready=true; break
  fi
  sleep 2
done
[[ $ready == true ]] || { echo 'API E2E não ficou pronta.' >&2; exit 1; }
export E2E_BASE_URL="http://$app:8080"
export E2E_API_URL=$E2E_BASE_URL
export E2E_TEST_USER_EMAIL=admin@prisma.example.invalid
docker run --rm --network "$network" --ipc=host \
  -e CI=true -e E2E_BASE_URL -e E2E_API_URL -e E2E_TEST_USER_EMAIL -e E2E_TEST_USER_PASSWORD \
  -v "$root:/workspace" -w /workspace/src/Prisma.Workspace.Web \
  mcr.microsoft.com/playwright:v1.62.1-noble \
  sh -c 'npm ci --no-audit && npm run e2e'
