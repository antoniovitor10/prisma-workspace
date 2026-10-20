#!/usr/bin/env bash
# Carregado pelos scripts locais; não aceita outros projetos/bancos do servidor.
prisma_server_profile=${1:-dev}
case "$prisma_server_profile" in
  dev)
    prisma_server_project=prisma-dev
    prisma_server_env=.env.server-dev
    prisma_server_http=3100
    prisma_server_sql=3102
    prisma_server_database=DetranKanban_E2E
    ;;
  qa)
    prisma_server_project=prisma-qa
    prisma_server_env=.env.server-qa
    prisma_server_http=3110
    prisma_server_sql=3111
    prisma_server_database=PrismaWorkspace_QA
    ;;
  *) echo 'Perfil recusado: use dev ou qa.' >&2; exit 1 ;;
esac

prisma_server_load_env() {
  set -a
  source "$prisma_server_env"
  set +a
  export PRISMA_SQL_PORT=${PRISMA_SQL_PORT:-$prisma_server_sql}
  [[ "$PRISMA_HTTP_PORT" == "$prisma_server_http" && "$PRISMA_SQL_PORT" == "$prisma_server_sql"
    && "$PRISMA_DB_NAME" == "$prisma_server_database" ]] || {
    echo 'Portas ou banco incompatíveis com o perfil local; operação recusada.' >&2; exit 1;
  }
}
