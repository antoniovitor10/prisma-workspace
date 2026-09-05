# Instalação local com Docker

Este quickstart cria a aplicação, o SQL Server e dois volumes persistentes sem depender da infraestrutura da Nordevs.

## Pré-requisitos

- Docker Desktop ou Docker Engine com Docker Compose v2;
- pelo menos 4 GB de memória disponíveis para os containers;
- PowerShell 7/Windows PowerShell ou shell POSIX com OpenSSL;
- porta 8080 livre, ou outra porta definida em `.env`.

## Licença do banco

O perfil padrão usa SQL Server 2022 Developer. Ele é gratuito somente para desenvolvimento e testes e **não pode ser
usado em produção**. Ao executar o script com a opção de aceite, você confirma a EULA da Microsoft. Para produção,
configure uma edição devidamente licenciada ou uma instância SQL Server externa antes do deploy.

## Subir o ambiente

No PowerShell:

```powershell
.\scripts\setup-docker.ps1 -AcceptSqlServerEula
```

No Linux/macOS:

```bash
./scripts/setup-docker.sh --accept-sql-server-eula
```

Os scripts criam um `.env` local com senhas aleatórias, constroem a imagem e aguardam os healthchecks. Banco,
anexos e chaves de proteção de dados usam volumes persistentes. Depois, abra <http://localhost:8080>. Para acompanhar
a inicialização:

```bash
docker compose ps
docker compose logs -f app
```

## Parar e reiniciar

```bash
docker compose stop
docker compose start
```

`docker compose down` remove os containers e a rede, mas preserva os volumes. Não use `down --volumes` se quiser
manter o banco e os anexos.

## Limitações desta etapa

- O fluxo seguro de criação do primeiro administrador ainda será entregue em um lote próprio, com contrato e testes.
- O endpoint `/health` valida o processo HTTP; o Compose também exige que o SQL Server esteja saudável antes de
  iniciar a aplicação.
- Backup, restauração, atualização e rollback serão documentados após ensaio automatizado.

## Diagnóstico

- Banco não inicia: confirme memória disponível, arquitetura suportada e política da senha gerada.
- Porta ocupada: altere `PRISMA_HTTP_PORT` no `.env` e execute `docker compose up -d` novamente.
- Estado do banco: `docker compose logs database`.
- Estado da aplicação: `docker compose logs app`.

Os segredos do `.env` nunca devem ser enviados ao Git, issues ou logs. Em produção, use o mecanismo de segredos da
plataforma de implantação e não exponha a porta 1433 publicamente.

## Referências oficiais

- [Executar SQL Server 2022 em container](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/quickstart-install-docker?view=sql-server-ver17)
- [Configurar variáveis do SQL Server no Linux](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-configure-environment-variables?view=sql-server-ver17)
- [Ordem de inicialização e `service_healthy` no Compose](https://docs.docker.com/compose/how-tos/startup-order/)
- [Gerenciamento de segredos no Docker Compose](https://docs.docker.com/compose/how-tos/use-secrets/)
