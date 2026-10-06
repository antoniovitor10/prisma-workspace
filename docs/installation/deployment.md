# Deploy desta instalação

O workflow `CI` publica `main` em `https://prisma.nordevs.com.br` depois de build backend,
lint/testes/build frontend, governança, imagem e E2E aprovados. PRs validam a mesma bateria,
sem acessar segredos de publicação. `workflow_dispatch` na main permite repetir o processo.
O pipeline não cancela uma publicação em andamento.

A imagem tem tag e label do commit, checksum e manifesto das migrations. O E2E usa a imagem
que será publicada, SQL Server descartável e Playwright desktop/mobile. Não usa banco ou
credenciais de produção. Logs e traces de navegador não são publicados como artefatos.

## Configuração privada

Secrets do repositório: `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY` e `DEPLOY_KNOWN_HOSTS`.
A chave é dedicada ao Prisma; o host SSH é verificado por chave conhecida. Valores e chave
privada não pertencem ao Git. O ambiente GitHub `production` registra as publicações.

Esta instalação usa `/home/dev/prisma-deploy` para entrada de artefatos, lock de publicação,
manifesto aprovado (`migrations.json`) e marcador do commit atual (`current-commit`).
O contêiner público é `prisma-workspace-api`, na rede `slc_default`; o SQL Server existente
é `detran-kanban-db`, banco `PrismaWorkspace`. O script recusa portas/redes inesperadas.

O manifesto inicial foi comparado com as migrations da instalação e do banco. Se o manifesto
da candidata mudar, o deploy para antes de substituir a aplicação. Uma migration nova exige
G-MIGRATION, ensaio em base isolada, backup e atualização explícita desse manifesto pelo operador.
O pipeline não aprova migrations.

## Preservação e rollback

Antes da troca, o script faz backup SQL `COPY_ONLY` com `CHECKSUM` e `RESTORE VERIFYONLY`, e
copia os anexos para `/home/dev/backups/prisma/<data>-<commit>`. A configuração do contêiner
anterior permanece preservada nele, sem exportar segredos para artefatos ou logs.

Os volumes de chaves e logs são mantidos. Anexos passam a persistir em
`/home/dev/painel-projects/prisma-runtime/App_Data`; na primeira publicação a cópia final
é feita com a aplicação parada, preservando uploads posteriores ao backup inicial.
A pasta antiga de código da produção não é sobrescrita nem usada para reconstruir imagens.

Falha de saúde interna ou pública remove somente a candidata e restaura o contêiner anterior.
O nome preservado aparece no registro `rollback-container` do backup. Para um rollback manual,
pare e renomeie o contêiner atual, devolva esse nome ao anterior e inicie-o; preserve os dois e
os volumes. Como o manifesto bloqueia mudanças de schema, essa reversão não tenta desfazer migrations.

Não use `docker compose up --build` na pasta antiga para operar esta instalação: ela contém
alterações históricas não consolidadas. Não execute limpeza de imagens, volumes ou backups
sem conferir o que sustenta a produção e os rollbacks.
