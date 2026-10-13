#!/usr/bin/env bash
# Prepara o schema existente em banco xUnit separado e executa a suíte real.
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"
scripts/setup-e2e-database.sh
set -a
source .env.server-dev
set +a
export ConnectionStrings__DefaultConnection=$(cat .env.server-dev.testconnection)
[[ "$ConnectionStrings__DefaultConnection" == 'Server=127.0.0.1,3102;Database=Prisma_BoardColumns_ServerDev_Seeded;'* ]] || { echo 'Banco xUnit recusado.' >&2; exit 1; }
export Jwt__Key=$PRISMA_JWT_KEY Seed__DemoEnabled=true Seed__DemoPassword=$E2E_TEST_USER_PASSWORD Setup__Enabled=false
export ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:3104
export Serilog__MinimumLevel__Default=Warning Serilog__MinimumLevel__Override__Microsoft=Warning
mkdir -p .runlogs/server-dev
# Não reutiliza uma API que já esteja ocupando a porta.
python3 - <<'PYPORT'
import socket
with socket.socket() as sock:
    sock.bind(('127.0.0.1',3104))
PYPORT
dotnet src/Prisma.Workspace.Api/bin/Debug/net8.0/Prisma.Workspace.Api.dll > .runlogs/server-dev/xunit-init.log 2>&1 &
api_pid=$!
trap 'kill "$api_pid" 2>/dev/null || true; wait "$api_pid" 2>/dev/null || true' EXIT
ready=false
for attempt in $(seq 1 90); do
  kill -0 "$api_pid" 2>/dev/null || { echo 'Inicialização SQL falhou; veja xunit-init.log.' >&2; exit 1; }
  if curl --fail --silent http://127.0.0.1:3104/health > /dev/null; then ready=true; break; fi
  sleep 1
done
[[ "$ready" == true ]] || { echo 'API de preparação não ficou pronta.' >&2; exit 1; }
kill "$api_pid"
wait "$api_pid" || true
trap - EXIT
/usr/bin/time -f 'TIME=%e EXIT=%x' -o .runlogs/server-dev/xunit.time \
  dotnet test tests/Prisma.Workspace.Tests --no-build --no-restore "$@"
