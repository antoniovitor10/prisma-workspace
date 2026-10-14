# Desenvolvimento e testes no servidor Linux

Ambiente exclusivo desta tarefa: projeto Docker `prisma-dev`. Não usa serviços de produção.
Requer Docker Compose, Python 3, curl, GNU time, Node 22 e SDK .NET 8.

## Preparação

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
npm ci --prefix src/Prisma.Workspace.Web
npm install --prefix tools/context-explorer --package-lock=false
npm ci --prefix tools/context-explorer/web
scripts/run-local.sh up
```

`compose.server-dev.yaml` usa o Dockerfile existente: React compilado e API na mesma origem.
Apenas o container do app usa a rede do host para alcançar o fixture IA local, sem alterar
firewall ou rede de outros serviços. Escuta explicitamente em `0.0.0.0:3100`; SQL escuta somente em `127.0.0.1:3102` no host.
A porta 1433 fica interna ao container. A rede e os volumes recebem prefixo `prisma-dev`.
O proxy existente respondeu HTTP 401 (autenticação) em
o endereço de prévia do servidor de desenvolvimento, porta 3100 (o PO informa o host); use as credenciais de proxy já fornecidas pelo PO.
Não houve teste autenticado desse proxy nem alteração de Caddy.
Não usar `scripts/setup-docker.sh` sem adaptar o nome do projeto e as portas.

`setup-e2e-database.sh` gera valores locais a partir de `.env.example`, inicia apenas o SQL e
prepara arquivos privados com permissões restritas. Não remove bancos existentes.
`run-local.sh up` aplica as migrations existentes e o seed pelo boot normal da API.
O SQL Server Developer é usado exclusivamente para desenvolvimento/testes.

Credenciais: `admin@prisma.example.invalid`; senha em `.env.e2e.seedpassword` e
`src/Prisma.Workspace.Web/.env.e2e.local`. Valores compartilhados do ambiente em
`.env.server-dev`. Todos ignorados pelo Git; não copiar para relatórios.

## Verificações

```bash
dotnet restore Prisma.Workspace.sln
dotnet build Prisma.Workspace.sln --no-restore
scripts/test-server-dev.sh
cd src/Prisma.Workspace.Web
npx tsc -b
npm run lint
npm test -- --pool=threads --maxWorkers=1 --fileParallelism=false
npm run build
npx playwright install --with-deps
npm run e2e
cd ../..
npm test --prefix tools/context-explorer
python3 -m unittest discover -s scripts/tests
python3 infra/ai-bridge/test_bridge.py
```

`test-server-dev.sh` prepara `Prisma_BoardColumns_ServerDev_Seeded` com as migrations e o
seed existentes, inicia uma API temporária em `127.0.0.1:3104`, encerra somente seu PID e
executa xUnit. O nome exigido pelos testes SQL é diferente do banco Playwright
`DetranKanban_E2E`; os dois pertencem ao mesmo SQL exclusivo. A senha é local.
Logs e medições locais ficam em `.runlogs/server-dev`, ignorados pelo Git.

Playwright executa desktop e mobile em Chromium, headless, um worker e sem retries por
padrão local. O provedor falso dos testes IA escuta em `0.0.0.0:3103`; a API alcança esse
fixture por `127.0.0.1:3103` na rede do host. Não usa conta/modelo real.
O app precisa estar saudável antes da suíte. O relatório HTML fica em
`src/Prisma.Workspace.Web/playwright-report` e os artefatos em `test-results`.

`run-api-e2e.sh` permite iniciar somente a API no host, em primeiro plano e porta 3101,
usando o banco E2E exclusivo. Para usar Vite nesse modo, configure `VITE_API_URL` e as
URLs Playwright explicitamente para essa API/frontend. O fluxo principal deste documento
usa a SPA compilada e a API no mesmo container. Os scripts PowerShell foram preservados.

## Encerramento

```bash
scripts/run-local.sh down
# Equivalente explícito, preservando volumes:
docker compose -p prisma-dev --env-file .env.server-dev -f compose.server-dev.yaml down
```

Para reiniciar a imagem já construída:

```bash
docker compose -p prisma-dev --env-file .env.server-dev -f compose.server-dev.yaml up -d --wait
```

Não há comando global de limpeza. Não publicar a porta 1433 nem reutilizar nomes de produção.

## QA visual com banco separado

O perfil `qa` reutiliza o Compose com projeto `prisma-qa`, app em `0.0.0.0:3110`,
SQL exclusivo em `127.0.0.1:3111` e banco `PrismaWorkspace_QA`. Rede e volumes têm
prefixo próprio. As migrations e o seed oficiais são aplicados no boot; não usa
nem recria o banco `DetranKanban_E2E` do perfil `dev`.

```bash
scripts/run-local.sh up qa
scripts/run-local.sh down qa
```

O segundo argumento seleciona somente `dev` ou `qa`; sem argumento, o comportamento
continua sendo `prisma-dev` em 3100/3102. Scripts recusam portas/banco incompatíveis.
`setup-e2e-database.sh qa` não altera arquivos de conexão/login do Playwright.

QA usa `prisma-qa-app:2c3816c`, imagem local pinada com o mesmo produto da main 2c3816c;
`up qa` exige essa imagem já preparada e nunca reconstrói a partir da branch de testes.
Nesta preparação a imagem existente do desenvolvimento foi conferida contra os fontes
runtime da main e recebeu essa tag, sem copiar imagem/configuração de produção.

Login oficial do seed: `admin@prisma.example.invalid`. URL, e-mail e senha gerada ficam
em `.env.qa.login`; somente a senha também fica em `.env.qa.seedpassword`. Configuração
completa em `.env.server-qa`. Arquivos ignorados pelo Git, modo 600; não copiar valores
para notas, logs ou mensagens. Perfil QA não fornece ponte IA; seu endereço local
separado está reservado e as conexões não são copiadas do banco E2E.
