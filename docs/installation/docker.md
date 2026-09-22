# Instalação local com Docker

Este quickstart cria a aplicação, o SQL Server e três volumes persistentes (banco, anexos e chaves de proteção)
sem depender da infraestrutura da Nordevs. Não é necessário instalar .NET ou Node.js no host para este caminho.

## Pré-requisitos

- Git e Docker Desktop ou Docker Engine com Docker Compose v2, com o daemon iniciado;
- host Linux x86-64/AMD64, ou Windows x86-64 com Docker Desktop em containers Linux;
- pelo menos 4 GB de memória disponíveis para os containers;
- PowerShell 7/Windows PowerShell ou shell POSIX com OpenSSL;
- porta 8080 livre, ou outra porta definida em `.env`.

O SQL Server usado neste Compose requer x86-64. Macs com Apple Silicon/ARM não são um ambiente validado
para este conjunto; use uma máquina/VM x86-64 para o banco e valide a configuração antes de começar.

## Baixar o projeto

```bash
git clone https://github.com/antoniovitor10/prisma-workspace.git
cd prisma-workspace
docker info
docker compose version
```

Execute os próximos comandos na raiz clonada. Uma cópia nova usa dados locais vazios, sem contas ou dados de produção.

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

## Configurar a primeira conta

Uma instalação nova não cria usuário, organização ou dados de demonstração. O script grava em
`PRISMA_SETUP_TOKEN`, dentro do `.env` local, um token aleatório de uso administrativo. Não envie esse valor para
issues, chats, logs ou commits.

1. Abra <http://localhost:8080/setup>.
2. Consulte `PRISMA_SETUP_TOKEN` diretamente no `.env` local e informe-o somente no campo protegido da tela.
3. Informe nome e e-mail do primeiro administrador e nome/identificador da organização.
4. Conclua a configuração e entre com a conta criada.
5. Altere `PRISMA_SETUP_ENABLED=false` no `.env` e reaplique o serviço:

```bash
docker compose up -d app
```

O servidor também mantém um marcador persistente e irreversível: repetir a chamada, restaurar a flag ou perder a
resposta original não cria outro administrador inicial. Recuperação de acesso deve usar um procedimento
administrativo próprio; não remova dados do banco para tentar reabrir o setup.

O dataset demonstrativo é separado desse fluxo, permanece desabilitado por padrão e só pode ser habilitado
explicitamente em `Development` sobre um banco vazio.

## Parar e reiniciar

```bash
docker compose stop
docker compose start
```

`docker compose down` remove os containers e a rede, mas preserva os volumes. Não use `down --volumes` se quiser
manter o banco e os anexos.

## Aplicar alterações locais no código

O Compose empacota o código na imagem e não oferece hot reload. Depois de editar ou atualizar o código:

```bash
docker compose up -d --build --wait app
```

Não execute novamente o script de setup quando `.env` já existir. Ele recusa sobrescrever as credenciais.
Atualizações podem aplicar migrations; faça backup do seu banco local antes de experimentar mudanças de schema.
Para conferir a aplicação, abra `/health` no mesmo endereço e entre com a conta criada no setup.

## Limitações desta etapa

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
