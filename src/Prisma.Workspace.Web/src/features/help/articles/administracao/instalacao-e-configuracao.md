---
titulo: Instalação e configuração do servidor
resumo: Instalar com Docker Compose, primeiro acesso, administrador da instalação e o nome de cada configuração do servidor, sem valores de segredo.
ordem: 1
atualizado_em: 2026-11-27
revisar_ate: 2027-02-27
relacionados:
  - repositorios-e-git/github-app
  - integracoes/integracoes
---

Esta parte é para quem instala e mantém o Prisma WorkSpace. Ela cita só o **nome** de cada configuração e onde ela fica; nenhum valor de segredo aparece aqui nem em outro lugar da ajuda. Segredos ficam em variáveis de ambiente ou no arquivo `.env` do servidor, que nunca vai para o repositório.

## Instalar com Docker Compose {#instalar}

1. No servidor, na pasta do repositório, rode `./scripts/setup-docker.sh --accept-sql-server-eula` (no Windows: `.\scripts\setup-docker.ps1 -AcceptSqlServerEula`).
2. O script gera o `.env` com valores aleatórios, constrói a imagem, sobe o banco e o app e espera ficarem saudáveis. Ele não sobrescreve um `.env` existente.
3. Abra `http://localhost:8080` (ou a porta definida em `PRISMA_HTTP_PORT`).

Sem o aceite da licença, o script para com "Confirme a licença do SQL Server com --accept-sql-server-eula." A edição padrão do SQL Server é a Developer, que serve para desenvolvimento e teste, não para produção.

### Variáveis do .env {#variaveis-do-env}

| Variável | Para que serve |
|---|---|
| `MSSQL_SA_PASSWORD` | senha do administrador do SQL Server |
| `MSSQL_PID` | edição do SQL Server |
| `PRISMA_DB_NAME` | nome do banco |
| `PRISMA_HTTP_PORT` | porta do app no servidor |
| `PRISMA_JWT_KEY` | chave que assina as sessões |
| `PRISMA_SETUP_ENABLED` | liga ou desliga a configuração inicial |
| `PRISMA_SETUP_TOKEN` | código de instalação do primeiro acesso |
| `PRISMA_AI_BRIDGE_TOKEN` | credencial da ponte das CLIs de IA (perfil opcional `ai-cli`) |
| `PRISMA_AI_BRIDGE_ADAPTER` | adaptador da ponte de IA (perfil opcional `ai-cli`) |

## Primeiro acesso {#primeiro-acesso}

1. Abra `/setup` no navegador.
2. Em **Código de instalação**, informe o valor de `PRISMA_SETUP_TOKEN` (no servidor, no `.env`). Ele é enviado no cabeçalho `X-Prisma-Setup-Token`.
3. Preencha **Nome completo**, **E-mail**, **Senha** (pelo menos 10 caracteres, com maiúscula, minúscula, número e símbolo), **Nome da organização** e **Identificador (slug)**.
4. Clique em **Concluir configuração**.
5. Depois, desligue a configuração inicial: `PRISMA_SETUP_ENABLED=false` no `.env` e `docker compose up -d app`.

Erros: "Não foi possível autorizar a configuração inicial." (código errado) e "Esta instalação já foi configurada.".

## Administrador da instalação {#administrador-da-instalacao}

A conta criada no primeiro acesso é o **Administrador da instalação**. Esse papel não vem de nenhum perfil de organização: ele cuida das conexões globais de IA, dos limites e do consumo. Outras contas só ganham ou perdem esse papel pelo script `scripts/set-platform-administrator.ps1`, rodado no servidor.

## Configurações do app {#configuracoes-do-app}

No Docker Compose, as configurações do app são variáveis de ambiente com `__` no lugar de `:` (por exemplo `Jwt:Key` vira `Jwt__Key`).

| Configuração | Para que serve |
|---|---|
| `ConnectionStrings__DefaultConnection` | conexão com o banco |
| `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience` | assinatura das sessões |
| `Setup__Enabled`, `Setup__Token` | configuração inicial (vêm de `PRISMA_SETUP_ENABLED` e `PRISMA_SETUP_TOKEN`) |
| `Seed__DemoEnabled` | dados de demonstração (só em desenvolvimento) |
| `FrontendBaseUrl` | endereço público do Prisma, usado nos links de convite e no login OAuth da IA |
| `Cors__AllowedOrigins__0` | origem permitida do navegador |

### E-mail {#e-mail}

Convites e o portal externo enviam e-mail pela seção `PortalEmail`: `PortalEmail:Host`, `PortalEmail:Port`, `PortalEmail:Sender`, `PortalEmail:User`, `PortalEmail:Password` e `PortalEmail:EnableSsl`. Sem e-mail configurado o Prisma funciona normalmente: os convites mostram o link para copiar (veja [Convite sem e-mail](/ajuda/organizacao-e-equipes/organizacao#convite-sem-email)).

### Assistente de IA {#assistente-de-ia}

- `Ai:Enabled`: liga ou desliga a IA na instalação inteira (ligada por padrão). Desligada, o botão do assistente some.
- `Ai:BridgeUrl` e `Ai:BridgeToken`: endereço e credencial da ponte das CLIs de IA.
- As chaves dos provedores de IA não ficam em configuração: o Administrador da instalação cadastra cada conexão em **Configurações** > **Assistente de IA** > **Conexões da instalação**, e o Prisma guarda a chave cifrada, mostrando só os últimos 4 caracteres.
- O administrador da organização liga a IA em **IA nesta organização** e define as cotas (vazio usa o padrão da instalação; 0 é sem limite).

## Integrações e GitHub {#integracoes-e-github}

| Configuração | Para que serve |
|---|---|
| `MethodGit:Enabled` | habilita as Integrações. Desligada, o item Integrações some das configurações do projeto, todos os tokens do projeto são recusados e as rotas das Integrações (inclusive o webhook) respondem 404 "O método não está habilitado nesta instalação." |
| `MethodGit:GitHubApiBaseUrl` | endereço da API do GitHub. Fora de desenvolvimento e teste, vale só o endereço oficial |
| `MethodGit:Secrets:<nome>` | valores secretos da integração com o GitHub, um por nome |

Os campos **Referência da chave privada** e **Referência do segredo do webhook** da tela de repositórios guardam só o `<nome>`. Para cada nome, cadastre no servidor:

- a variável de ambiente `MethodGit__Secrets__<nome>` com o conteúdo da chave privada da GitHub App (o arquivo PEM inteiro), para a referência da chave;
- a variável `MethodGit__Secrets__<nome>` com um valor aleatório de pelo menos 32 bytes, exclusivo do repositório, para a referência do segredo do webhook. O mesmo valor vai no campo **Secret** do webhook no GitHub.

Prefira nomes com letras, números e sublinhado (alguns shells não exportam variáveis com hífen). Depois de mudar variáveis, reinicie o app para que ele leia os valores novos.

> Valores de segredo nunca entram em card, comentário, wiki, relatório, print ou arquivo versionado. Se um valor vazar, gere outro (nova chave privada no GitHub, novo segredo de webhook), atualize o servidor e o GitHub e revogue os tokens afetados.
