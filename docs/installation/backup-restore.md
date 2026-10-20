# Backup e restauração da Community Edition

Este roteiro é para uma instalação própria criada pelo [Compose local](docker.md), executado na raiz desse
clone. Usa os serviços `app` e `database` e o banco padrão `PrismaWorkspace`. Se você configurou outro nome,
substitua esse identificador nos comandos SQL. Confira o projeto com `docker compose ps` antes de operar.
Não execute estes exemplos no ambiente institucional ou em um projeto Docker diferente.

O conjunto recuperável inclui banco, anexos em `/app/App_Data`, chaves de proteção e a configuração da instalação.
Copiar somente o volume do SQL Server em funcionamento não substitui um backup SQL. Pare as gravações da aplicação
durante a captura para manter os registros e os arquivos no mesmo ponto. O SQL continua em execução.

## Capturar o conjunto

No shell POSIX, crie uma pasta privada fora do clone e pare somente a aplicação dessa instalação:

```bash
umask 077
prisma_snapshot_dir="$HOME/prisma-backups/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$prisma_snapshot_dir"
docker compose stop app
```

Faça uma cópia completa independente da sequência de backups diferenciais e verifique os checksums. O arquivo
`community-full.bak` dentro do container é temporário e será substituído na próxima captura; cada cópia no host
fica em uma pasta nova. A senha é lida dentro do container, sem ser colocada nos argumentos do `sqlcmd`:

```bash
docker compose exec -T database sh -c '
  export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"
  exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "
    BACKUP DATABASE [PrismaWorkspace]
      TO DISK=N'"'"'/var/opt/mssql/data/community-full.bak'"'"'
      WITH COPY_ONLY, CHECKSUM, INIT;
    RESTORE VERIFYONLY FROM DISK=N'"'"'/var/opt/mssql/data/community-full.bak'"'"' WITH CHECKSUM;
  "
'
docker compose cp database:/var/opt/mssql/data/community-full.bak "$prisma_snapshot_dir/database.bak"
docker compose cp app:/app/App_Data "$prisma_snapshot_dir/App_Data"
docker compose cp app:/root/.aspnet/DataProtection-Keys "$prisma_snapshot_dir/DataProtection-Keys"
cp .env "$prisma_snapshot_dir/configuration.env"
git rev-parse HEAD > "$prisma_snapshot_dir/commit.txt"
docker compose images > "$prisma_snapshot_dir/images.txt"
docker compose start app
```

Confira a saída de cada comando antes de seguir. Se um passo falhar, não trate a pasta parcial como backup válido.
Depois da cópia, valide `/health` e o login. Preserve a pasta completa em armazenamento privado com controle de acesso
e uma segunda cópia fora da máquina. Ela contém credenciais, dados pessoais e material de autenticação; não a envie
ao Git, issues ou logs. `VERIFYONLY` verifica a mídia, mas não substitui o ensaio de restauração descrito a seguir.

## Ensaiar a restauração sem substituir o banco original

Use uma cópia isolada da instalação, com outro projeto Compose, porta livre e volumes novos. Execute
`export COMPOSE_PROJECT_NAME=prisma-recovery` no shell do clone isolado e defina uma porta diferente em seu `.env`;
esse nome prevalece sobre `name` do Compose. Confira `docker compose ps` antes de continuar. Mantenha o serviço
`app` parado e inicie somente o `database` com `docker compose up -d --wait database`. A edição Developer do SQL Server continua restrita a desenvolvimento
e testes. Não execute setup nem inicie a aplicação sobre a base restaurada antes de conferir sua configuração.

Copie o backup para o container de destino e consulte os nomes lógicos dos arquivos:

```bash
docker compose cp "$prisma_snapshot_dir/database.bak" database:/var/opt/mssql/data/restore-input.bak
docker compose exec -T database sh -c '
  export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"
  exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "
    RESTORE VERIFYONLY FROM DISK=N'"'"'/var/opt/mssql/data/restore-input.bak'"'"' WITH CHECKSUM;
    RESTORE FILELISTONLY FROM DISK=N'"'"'/var/opt/mssql/data/restore-input.bak'"'"';
  "
'
```

Prepare `restore.sql` na pasta privada. No exemplo abaixo, substitua `PrismaWorkspace` e `PrismaWorkspace_log`
pelos **nomes lógicos retornados por FILELISTONLY**. Se houver mais arquivos, cada um precisa de seu próprio `MOVE`.
Os caminhos de destino precisam ser novos. Não adicione `REPLACE`: o roteiro recusa sobrescrever uma base existente.

```sql
IF DB_ID(N'PrismaWorkspaceRestored') IS NOT NULL
    THROW 51000, 'O banco de destino já existe; restauração cancelada.', 1;

RESTORE DATABASE [PrismaWorkspaceRestored]
FROM DISK=N'/var/opt/mssql/data/restore-input.bak'
WITH CHECKSUM,
  MOVE N'PrismaWorkspace' TO N'/var/opt/mssql/data/PrismaWorkspaceRestored.mdf',
  MOVE N'PrismaWorkspace_log' TO N'/var/opt/mssql/data/PrismaWorkspaceRestored_log.ldf';

DBCC CHECKDB([PrismaWorkspaceRestored]) WITH NO_INFOMSGS;
```

Execute sem imprimir credenciais:

```bash
docker compose exec -T database sh -c '
  export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"
  exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b
' < "$prisma_snapshot_dir/restore.sql"
```

Com a aplicação ainda parada, copie o conteúdo dos diretórios para os volumes novos, preservando a estrutura:

```bash
docker compose create app
docker compose cp "$prisma_snapshot_dir/App_Data/." app:/app/App_Data
docker compose cp "$prisma_snapshot_dir/DataProtection-Keys/." app:/root/.aspnet/DataProtection-Keys
```

Antes do comando `create`, confira o commit registrado no backup e construa ou carregue sua imagem correspondente
nesse clone isolado. O container `app` precisa existir para o `compose cp`; `docker compose create app` o cria sem
iniciar o processo. O SQL de destino precisa aceitar backups da versão de origem; não restaure em uma versão anterior.
Confira a configuração privada do destino: banco `PRISMA_DB_NAME=PrismaWorkspaceRestored`, senha correspondente
ao SQL de destino, chave JWT original, setup desabilitado e URLs/porta da instalação isolada. Não sobrescreva
cegamente o `.env` novo com a configuração do servidor antigo. Depois, inicie a mesma versão da aplicação registrada
no backup com `docker compose up -d --no-build app`.

Valide a integridade do banco, quantidade de organizações/usuários/tarefas, login existente, um anexo conhecido e
conteúdo da wiki. Um HTTP200 em `/health` sozinho não comprova recuperação dos dados. Registre o commit, os checksums
dos arquivos, os resultados e a data do ensaio fora do repositório.

## Atualização e rollback

Antes de atualizar, faça e ensaie esse conjunto. Guarde a imagem/commit anterior e leia as migrations da versão
candidata. Uma versão anterior do aplicativo pode ser incompatível com um schema mais novo; não faça downgrade
apontando-a para o banco já atualizado. A recuperação completa usa banco e arquivos do mesmo backup em destino
isolado e a imagem correspondente. Gravações posteriores ao snapshot não estarão nesse conjunto. A troca final de
ambiente, política de retenção e publicação precisam de autorização do operador responsável.

## Evidência e limites

O ensaio da Oficina usa somente banco e arquivos sintéticos próprios de QA. Os resultados reais ficam no relatório
`relatorios/oficina-backup-docs.md`; este roteiro não declara homologação de instalação institucional ou upgrade
entre versões diferentes. A homologação ampla da TASK-507 continua separada.

Referências: [backup SQL](https://learn.microsoft.com/en-us/sql/t-sql/statements/backup-transact-sql?view=sql-server-ver17),
[restauração em container Linux](https://learn.microsoft.com/en-us/sql/linux/tutorial-restore-backup-in-sql-server-container?view=sql-server-ver17),
[VERIFYONLY](https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-verifyonly-transact-sql?view=sql-server-ver17)
e [Compose cp](https://docs.docker.com/reference/cli/docker/compose/cp/).
