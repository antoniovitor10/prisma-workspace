# prisma-mcp

Adaptador MCP (stdio) que liga o método de trabalho ao Prisma WorkSpace: o repositório
continua sendo a verdade do **conteúdo** (HU, task, critérios) e o Prisma passa a ser a
verdade do **status** e do histórico. Fase 1 do rastreador `prisma` (decisão D100).

O pacote tem dois executáveis:

- `prisma-mcp` (`dist/server.js`): servidor MCP com as 8 operações do contrato.
- `prisma-metodo-import` (`dist/import.js`): importa `docs/trabalho/HU-*` para o Prisma.

Os dois só chamam a API REST que já existe no Prisma. Não há migration nem mudança na API.

## Operações do MCP

| Ferramenta | Parâmetros | Para quê |
|---|---|---|
| `regras_do_projeto` | nenhum | status, matriz de transições do papel atual e convenções; chamar no início da sessão |
| `obter_task` | `id` | status, título, arquivo e PR de uma task |
| `listar_tasks` | `status?` | tasks do projeto, opcionalmente filtradas por status |
| `proximas_tasks` | nenhum | tasks em `em-desenvolvimento` com todas as dependências concluídas |
| `criar_task` | `id, titulo, hu, arquivo, cobre, risco, status?` | cria a task no quadro, marcada com a HU |
| `mover_status` | `id, novo_status, motivo` | muda a coluna, conforme a matriz do papel |
| `comentar` | `id, texto` | relatório de execução, bloqueio, handoff |
| `vincular_pr` | `id, url` | grava o campo PR e comenta |

Status (uma coluna para cada, nesta ordem): `backlog` (Backlog), `pronta` (Pronta),
`em-desenvolvimento` (Em desenvolvimento), `em-revisao` (Em revisão), `em-teste` (Em teste),
`concluida` (Concluída) e `bloqueada` (Bloqueada).

Papel `agente` (padrão):

- pode mover para `bloqueada` a partir de qualquer status, exceto `concluida`;
- pode mover `em-desenvolvimento` para `em-revisao` (nível 0, sem push);
- pode voltar `bloqueada` para `em-desenvolvimento` somente de um bloqueio que ele mesmo registrou;
- **nunca autoriza**: `backlog`/`pronta` para `em-desenvolvimento` é recusado.

Papel `humano`: qualquer transição.

Cada `mover_status` grava um comentário `[status] <de> -> <para> · <papel> · mcp · <motivo>`,
para o histórico do card mostrar quem mudou, quando e por qual canal.

No Prisma, cada task é um card de topo no quadro, e muda de coluna ao vivo com `mover_status`.
A HU não vira card: ela fica no campo personalizado **HU** (por exemplo `HU-0001`) e numa tag com o
mesmo nome em cada task; o status da HU continua no `hu.md`. `depende_de` vira vínculo de
dependência entre as tasks. A chave de cada task é o campo personalizado **ID do metodo**
(`T-0001`); o título do card (`T-0001 · <título>`) é só para leitura. Os outros campos do projeto
são **Arquivo**, **Cobre**, **Risco** e **PR**.

## Variáveis de ambiente

A configuração vem só de variáveis de ambiente. Não grave credencial em arquivo versionado.

| Variável | Obrigatória | Descrição |
|---|---|---|
| `PRISMA_URL` | sim | URL do Prisma, por exemplo `http://127.0.0.1:13100` |
| `PRISMA_EMAIL` e `PRISMA_PASSWORD` | uma das duas formas | login do usuário que o adaptador usa |
| `PRISMA_TOKEN` | uma das duas formas | token JWT no lugar de e-mail e senha |
| `PRISMA_PROJECT` | sim | nome do projeto no Prisma, por exemplo `canais-mobile` |
| `PRISMA_BOARD` | não | nome do quadro; padrão = nome do projeto |
| `PRISMA_ROLE` | não | `agente` (padrão) ou `humano` |

O adaptador não escreve token, senha nem cabeçalho de autorização em log ou em mensagem de erro.

## Compilar

Requer Node 22.

```
cd tools/prisma-mcp
npm ci
npm run build
```

O resultado fica em `dist/`. Testes: `npm test` (unitários; o de integração só roda com as
variáveis da seção [Testes](#testes)).

## Importar as tasks do repositório

```
node dist/import.js <pasta docs/trabalho> [--dry-run] [--sincronizar-status]
```

- Cria o projeto, o quadro, as 7 colunas e os campos personalizados se faltarem.
- Cria ou atualiza cada task pela chave **ID do metodo**, com o campo **HU** e a tag da HU. A HU
  não vira card. Rodar duas vezes não duplica nada.
- O `status:` do frontmatter só é usado na criação do item; depois disso o Prisma é a verdade.
  Com `--sincronizar-status`, o status do frontmatter é reaplicado em todos os itens.
- `--dry-run` mostra o que faria sem gravar.
- No fim imprime um resumo: criados, atualizados, sem mudança.

A importação é uma ferramenta do desenvolvedor: rode com `PRISMA_ROLE=humano`. Com papel `agente`
ela segue a mesma matriz do MCP.

Depois da importação, tire o `status:` do frontmatter das tasks: em modo prisma ele não existe.
O status da HU continua em `hu.md`.

### Onde ver no Prisma

- **Kanban** do projeto: cada task é um card na coluna do seu status; a tag mostra a HU.
- **Itens** do projeto: lista as tasks com a coluna **Etapa**.
- Detalhe do card: abas **Comentários** (relatórios, bloqueios e os comentários `[status]`) e
  **Histórico** (quem moveu, quando).

Exemplo no PowerShell, com o túnel aberto (ver abaixo):

```powershell
$env:PRISMA_URL = "http://127.0.0.1:13100"
$env:PRISMA_PROJECT = "canais-mobile"
$env:PRISMA_ROLE = "humano"
$env:PRISMA_EMAIL = Read-Host "E-mail"
$env:PRISMA_PASSWORD = Read-Host "Senha" -MaskInput
node dist\import.js C:\caminho\do\repo\docs\trabalho --dry-run
node dist\import.js C:\caminho\do\repo\docs\trabalho
```

## Configurar num PC Windows

O Prisma de desenvolvimento roda no servidor e só escuta em `127.0.0.1`. No PC, abra um túnel SSH
que leva a porta local 13100 até ele e deixe o terminal aberto:

```powershell
ssh -N -L 13100:127.0.0.1:3100 <usuario>@<servidor-dev>
```

Confira com `curl.exe http://127.0.0.1:13100/health`.

Compile o pacote no PC (seção [Compilar](#compilar)). Nos exemplos abaixo o caminho é
`C:\dev\prisma-workspace\tools\prisma-mcp\dist\server.js`; troque pelo seu.

### Credenciais fora dos arquivos

Guarde e-mail e senha como variáveis de ambiente do usuário do Windows (uma vez só; abra um
terminal novo depois):

```powershell
[Environment]::SetEnvironmentVariable("PRISMA_EMAIL", "voce@exemplo.com", "User")
[Environment]::SetEnvironmentVariable("PRISMA_PASSWORD", (Read-Host "Senha" -MaskInput), "User")
```

Os clientes MCP abaixo repassam essas variáveis ao servidor sem que elas apareçam em nenhum
arquivo do repositório.

### Claude Code

Opção 1, por comando, com escopo local. Rode na pasta do repositório do projeto; a configuração
vale só para ele e fica no seu `%USERPROFILE%\.claude.json`, fora do repositório:

```powershell
claude mcp add --scope local `
  --env PRISMA_URL=http://127.0.0.1:13100 `
  --env PRISMA_PROJECT=canais-mobile `
  --env PRISMA_ROLE=agente `
  prisma -- node C:\dev\prisma-workspace\tools\prisma-mcp\dist\server.js
```

`PRISMA_EMAIL` e `PRISMA_PASSWORD` não entram no comando nem em arquivo nenhum: o servidor herda
o ambiente do terminal em que o Claude Code foi aberto, onde as variáveis do usuário já estão.
Abra o Claude Code num terminal novo depois de criar as variáveis.

Opção 2, `.mcp.json` na raiz do repositório do projeto (pode ser versionado, porque só referencia
as variáveis com `${...}`; o Claude Code as expande ao subir o servidor):

```json
{
  "mcpServers": {
    "prisma": {
      "command": "node",
      "args": ["C:\\dev\\prisma-workspace\\tools\\prisma-mcp\\dist\\server.js"],
      "env": {
        "PRISMA_URL": "http://127.0.0.1:13100",
        "PRISMA_PROJECT": "canais-mobile",
        "PRISMA_ROLE": "agente",
        "PRISMA_EMAIL": "${PRISMA_EMAIL}",
        "PRISMA_PASSWORD": "${PRISMA_PASSWORD}"
      }
    }
  }
}
```

Confira com `claude mcp list` (deve mostrar `prisma` conectado) e, dentro de uma sessão, `/mcp`.

### Codex

Em `%USERPROFILE%\.codex\config.toml`:

```toml
[mcp_servers.prisma]
command = "node"
args = ["C:\\dev\\prisma-workspace\\tools\\prisma-mcp\\dist\\server.js"]
# repassa as variáveis do usuário do Windows, sem gravar o valor aqui
env_vars = ["PRISMA_EMAIL", "PRISMA_PASSWORD"]

[mcp_servers.prisma.env]
PRISMA_URL = "http://127.0.0.1:13100"
PRISMA_PROJECT = "canais-mobile"
PRISMA_ROLE = "agente"
```

Confira com `codex mcp list`. Se a sua versão do Codex não tiver `env_vars`, coloque
`PRISMA_EMAIL` e `PRISMA_PASSWORD` dentro de `[mcp_servers.prisma.env]`: esse arquivo é seu, fica
fora do repositório, mas guarda a senha em texto puro.

### Primeiro uso

Numa sessão nova do agente: "chame regras_do_projeto e depois listar_tasks". Se o MCP não
responder, o agente para e avisa; ele não volta para o modo local.

## Testes

```
npm test
```

Roda os unitários (mapa de status, matriz de papéis, configuração e leitura dos arquivos do método
com os dados reais de `canais-mobile`).

`test/ensaio-demo.mjs` repete o roteiro da demonstração (importa duas vezes e usa o MCP como agente e
como humano) num projeto que você informa: `node test/ensaio-demo.mjs <projeto> <pasta docs/trabalho>`.

O teste de integração (`test/integration.test.ts`) fala com um Prisma de verdade, num projeto
próprio criado para o teste (nunca um projeto real). Ele importa duas vezes e confere que nada
duplicou, sobe o servidor MCP por stdio com o cliente do SDK, chama as 8 operações e confere que o
agente não consegue autorizar uma task. Só roda com:

```
PRISMA_IT=1 PRISMA_URL=http://127.0.0.1:3100 PRISMA_EMAIL=... PRISMA_PASSWORD=... npm test
```

Opcional: `PRISMA_IT_PROJECT` (padrão `metodo-mcp-teste-<data e hora>`). A HU importada no teste
é a de exemplo em `test/fixtures/trabalho`.

## Limites desta fase

- A matriz do papel `agente` é aplicada pelo adaptador. Levar essa checagem para a API está pendente.
- Sem webhooks do Git (fase 2) e sem gate de merge (fase 3).
