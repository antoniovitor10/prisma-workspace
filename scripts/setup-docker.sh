#!/usr/bin/env sh
set -eu

if [ "${1:-}" != "--accept-sql-server-eula" ]; then
  echo "Confirme a licença do SQL Server com --accept-sql-server-eula." >&2
  echo "Consulte docs/installation/docker.md." >&2
  exit 1
fi

command -v docker >/dev/null 2>&1 || { echo "Docker não encontrado no PATH." >&2; exit 1; }
command -v openssl >/dev/null 2>&1 || { echo "OpenSSL não encontrado no PATH." >&2; exit 1; }
docker compose version >/dev/null

workspace=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
env_path="$workspace/.env"
if [ -e "$env_path" ]; then
  echo ".env já existe. Preserve seus segredos ou remova o arquivo conscientemente." >&2
  exit 1
fi

sql_password="Pr1!$(openssl rand -hex 24)"
jwt_key=$(openssl rand -hex 48)
umask 077
{
  printf '%s\n' "MSSQL_SA_PASSWORD=$sql_password"
  printf '%s\n' "PRISMA_JWT_KEY=$jwt_key"
  printf '%s\n' 'MSSQL_PID=Developer'
  printf '%s\n' 'PRISMA_DB_NAME=PrismaWorkspace'
  printf '%s\n' 'PRISMA_HTTP_PORT=8080'
} > "$env_path"

echo "Arquivo .env criado com segredos aleatórios e mantido fora do Git."
cd "$workspace"
docker compose up --build --detach --wait
echo "Prisma WorkSpace disponível em http://localhost:8080"
