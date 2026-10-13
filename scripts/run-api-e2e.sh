#!/usr/bin/env bash
# Alternativa Linux da API E2E em primeiro plano, usando SQL do prisma-dev.
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"
[[ -f .env.e2e.connection && -f .env.e2e.seedpassword ]] || { echo 'Execute scripts/setup-e2e-database.sh.' >&2; exit 1; }
export ConnectionStrings__DefaultConnection=$(cat .env.e2e.connection)
[[ "$ConnectionStrings__DefaultConnection" == 'Server=127.0.0.1,3102;Database=DetranKanban_E2E;'* ]] || { echo 'Banco E2E exclusivo recusado.' >&2; exit 1; }
set -a
source .env.server-dev
set +a
export Jwt__Key=$PRISMA_JWT_KEY Seed__DemoEnabled=true
export Seed__DemoPassword=$(cat .env.e2e.seedpassword)
export Setup__Enabled=false ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=${1:-http://127.0.0.1:3101}
export Cors__AllowedOrigins__0=${2:-http://127.0.0.1:3100}
export RateLimiting__GlobalPermitLimit=10000 RateLimiting__AuthPermitLimit=10000
export Serilog__MinimumLevel__Default=Warning Serilog__MinimumLevel__Override__Microsoft=Warning
exec env 'Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command=Warning' dotnet run --project src/Prisma.Workspace.Api --no-launch-profile
