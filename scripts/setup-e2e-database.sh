#!/usr/bin/env bash
# Gera configuração privada e prepara SQL exclusivo do perfil dev ou qa.
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"
umask 077
source scripts/server-profile.sh "${1:-dev}"
if [[ ! -f "$prisma_server_env" ]]; then
  PRISMA_SERVER_PROFILE="$prisma_server_profile" python3 - <<'PYENV'
from pathlib import Path
import os, secrets
qa = os.environ['PRISMA_SERVER_PROFILE'] == 'qa'
values = dict(line.split('=', 1) for line in Path('.env.example').read_text().splitlines() if line and not line.startswith('#'))
values.update(MSSQL_SA_PASSWORD='Dev1!'+secrets.token_hex(24), PRISMA_JWT_KEY=secrets.token_hex(48), PRISMA_SETUP_TOKEN=secrets.token_urlsafe(32), PRISMA_HTTP_PORT='3110' if qa else '3100', PRISMA_SQL_PORT='3111' if qa else '3102', PRISMA_DB_NAME='PrismaWorkspace_QA' if qa else 'DetranKanban_E2E', PRISMA_SETUP_ENABLED='false', E2E_TEST_USER_PASSWORD='E2e1!'+secrets.token_hex(24), PRISMA_AI_BRIDGE_TOKEN=secrets.token_hex(32))
if qa:
    values.update(PRISMA_SERVER_IMAGE='prisma-qa-app:2c3816c', PRISMA_AI_BRIDGE_URL='http://127.0.0.1:3119/v1')
Path('.env.server-qa' if qa else '.env.server-dev').write_text(''.join(f'{k}={v}\n' for k,v in values.items()))
PYENV
fi
prisma_server_load_env
chmod 600 "$prisma_server_env"
if [[ "$prisma_server_profile" == qa ]]; then
  printf '%s\n' "$E2E_TEST_USER_PASSWORD" > .env.qa.seedpassword
  printf 'QA_URL=http://127.0.0.1:3110\nE2E_TEST_USER_EMAIL=admin@prisma.example.invalid\nE2E_TEST_USER_PASSWORD=%s\n' "$E2E_TEST_USER_PASSWORD" > .env.qa.login
  chmod 600 .env.qa.seedpassword .env.qa.login
else
printf '%s\n' "Server=127.0.0.1,3102;Database=DetranKanban_E2E;User Id=sa;Password=$MSSQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True" > .env.e2e.connection
printf '%s\n' "$E2E_TEST_USER_PASSWORD" > .env.e2e.seedpassword
printf '%s\n' "Server=127.0.0.1,3102;Database=Prisma_BoardColumns_ServerDev_Seeded;User Id=sa;Password=$MSSQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True" > .env.server-dev.testconnection
cat > src/Prisma.Workspace.Web/.env.e2e.local <<EOF
E2E_BASE_URL=http://127.0.0.1:3100
E2E_API_URL=http://127.0.0.1:3100
E2E_TEST_USER_EMAIL=admin@prisma.example.invalid
E2E_TEST_USER_PASSWORD=$E2E_TEST_USER_PASSWORD
E2E_AI_PROVIDER_HOST=127.0.0.1
E2E_AI_PROVIDER_PORT=3103
EOF
fi
docker compose -p "$prisma_server_project" --env-file "$prisma_server_env" -f compose.server-dev.yaml up -d --wait database
# Program.cs aplica migrations existentes e seed ao iniciar a API.
# Não apaga bancos existentes nem altera migrations.
echo "SQL exclusivo do $prisma_server_project pronto; configuração privada criada."
