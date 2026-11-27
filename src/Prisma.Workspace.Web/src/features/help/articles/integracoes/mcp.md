---
titulo: Ligar o servidor MCP
resumo: Instalar o servidor MCP do Prisma, configurar as variáveis de ambiente no cliente de IA e conhecer as operações.
ordem: 3
atualizado_em: 2026-11-27
revisar_ate: 2027-02-27
relacionados:
  - integracoes/tokens
  - integracoes/integracoes
---

O servidor MCP do Prisma deixa um agente de IA (Claude Code, Codex e outros clientes MCP) ler as regras do projeto, consultar e criar tasks, mover status e vincular branches e PRs, sempre com um token do projeto.

## Antes de começar {#antes-de-comecar}

- A integração do projeto precisa estar **ativa** (veja [Ativar ou pausar](/ajuda/integracoes/integracoes#ativar-ou-pausar)).
- Emita um token com o papel **Agente** (ou **Humano pessoal**, para você mesmo) e guarde o valor (veja [Emitir um token](/ajuda/integracoes/tokens#emitir-um-token)).
- Tenha o Node 22 ou mais novo na máquina do agente.

## Instalar {#instalar}

O servidor fica na pasta `tools/prisma-mcp` do repositório do Prisma WorkSpace:

```
cd tools/prisma-mcp
npm ci
npm run build
```

O executável é `dist/server.js` (também roda com `npm start`).

## Variáveis de ambiente {#variaveis-de-ambiente}

| Variável | Obrigatória | O que é |
|---|---|---|
| `PRISMA_URL` | sim | endereço do Prisma (http ou https, sem usuário, senha ou parâmetros) |
| `PRISMA_TOKEN` | sim | o token do projeto, começando com `pmt_` |
| `PRISMA_PROJECT` | sim | nome ou ID do projeto |
| `PRISMA_BOARD` | não | quadro esperado; só confere com o do token |
| `PRISMA_ROLE` | não | agente, humano ou sistema; se diferir do papel do token, o servidor para |

Guarde o token numa variável de ambiente do seu usuário, nunca num arquivo do repositório. O login do navegador (e-mail e senha) não é aceito: "Configure PRISMA_TOKEN com o token opaco emitido pela API do método."

## Configurar o cliente {#configurar-o-cliente}

### Claude Code {#claude-code}

Num arquivo `.mcp.json` na raiz do repositório do projeto, referencie a variável com `${PRISMA_TOKEN}` em vez do valor (assim o arquivo pode ser versionado):

```
{
  "mcpServers": {
    "prisma": {
      "command": "node",
      "args": ["<caminho>/tools/prisma-mcp/dist/server.js"],
      "env": {
        "PRISMA_URL": "<endereco do Prisma>",
        "PRISMA_PROJECT": "<projeto>",
        "PRISMA_ROLE": "agente",
        "PRISMA_TOKEN": "${PRISMA_TOKEN}"
      }
    }
  }
}
```

Confira com `claude mcp list` (deve mostrar `prisma` conectado).

### Codex {#codex}

No `config.toml` do Codex, crie a seção `[mcp_servers.prisma]` com `command = "node"`, o caminho de `dist/server.js` em `args` e `env_vars = ["PRISMA_TOKEN"]`, para repassar a variável sem gravar o valor. As demais variáveis vão em `[mcp_servers.prisma.env]`.

## Operações {#operacoes}

| Operação | O que faz |
|---|---|
| `regras_do_projeto` | lê as regras e os status permitidos do projeto |
| `obter_task` | lê uma task pelo ID |
| `listar_tasks` | lista as tasks, com filtro opcional de status |
| `proximas_tasks` | mostra as próximas tasks a pegar |
| `criar_task` | cria uma task (ID, título, HU, arquivo, cobertura, risco) |
| `mover_status` | move a task, sempre com um motivo |
| `comentar` | comenta na task |
| `vincular_pr` | liga um pull request do GitHub à task |
| `vincular_branch` | liga uma branch à task |

## Quando dá erro 401 ou 403 {#erro-401-ou-403}

O servidor MCP não tenta de novo: ele para e avisa. As causas mais comuns são integração pausada, token vencido ou revogado e projeto fora do status Ativo (veja [Por que a integração pausada recusa todo token](/ajuda/integracoes/integracoes#pausada-recusa-token)).
