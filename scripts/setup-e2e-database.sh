#!/usr/bin/env bash
# Gera configuração privada e prepara SQL Server exclusivo do prisma-dev.
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"
umask 077
if [[ ! -f .env.server-dev ]]; then
  python3 - <<'PYENV'
from pathlib import Path
import secrets
values = dict(line.split('=', 1) for line in Path('.env.example').read_text().splitlines() if line and not line.startswith('#'))
values.update(MSSQL_SA_PASSWORD='Dev1!'+secrets.token_hex(24), PRISMA_JWT_KEY=secrets.token_hex(48), PRISMA_SETUP_TOKEN=secrets.token_urlsafe(32), PRISMA_HTTP_PORT='3100', PRISMA_DB_NAME='DetranKanban_E2E', PRISMA_SETUP_ENABLED='false', E2E_TEST_USER_PASSWORD='E2e1!'+secrets.token_hex(24), PRISMA_AI_BRIDGE_TOKEN=secrets.token_hex(32))
Path('.env.server-dev').write_text(''.join(f'{k}={v}\n' for k,v in values.items()))
PYENV
fi
set -a
source .env.server-dev
set +a
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
docker compose -p prisma-dev --env-file .env.server-dev -f compose.server-dev.yaml up -d --wait database
# Program.cs aplica migrations existentes e seed ao iniciar a API.
# Não apaga bancos existentes nem altera migrations.
echo 'SQL exclusivo pronto; configuração privada criada. Inicie scripts/run-local.sh.'
