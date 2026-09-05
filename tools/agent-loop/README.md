# Agent Loop V0

Motor local determinístico para controlar tarefas, transições, validações, falhas e Human Gates. Usa apenas módulos built-in do Node.js.

## Uso

```text
node cli.js <command> [arguments] [--store DIR] [--telemetry DIR] [--agent ID]
```

`--store` troca o diretório de envelopes (padrão: `.agent-state/tasks`). `--telemetry` troca o diretório de eventos (padrão: `.agent-state/telemetry`).

## Comandos

```text
create <id> <title> <objective> [overrides-json]
validate <id>
status <id>
advance <id> [to-state]
transition <id> <to-state>
record-gate <id> <gate> <pass|fail> [details-json]
fail <id> <code> [details-json]
block <id> <reason>
resume <id>
request-human-gate <id> <gate> <reason>
approve <id> <gate-or-id> <resolved-by> [note]
reject <id> <gate-or-id> <resolved-by> [note]
checkpoint <id> [label] [data-json]
run-gates <id> [gate1,gate2,...]
states
help
```

Exemplos:

```text
node cli.js create TASK-1 "Título" "Objetivo"
node cli.js transition TASK-1 spec-draft
node cli.js request-human-gate TASK-1 G-SPEC "Spec requer aprovação"
node cli.js approve TASK-1 G-SPEC PO "Aprovada"
node cli.js checkpoint TASK-1 local '{"stage":"ready"}'
node cli.js run-gates TASK-1
```

No PowerShell, adapte as aspas de JSON conforme necessário.

## API

`lib/engine.js` exporta a API consolidada:

- Envelope: `createEnvelope`, `touch`, `cloneEnvelope`.
- Store: `saveTask`, `loadTask`, `taskExists`, `listTasks`, `deleteTask`.
- Telemetria: `appendEvent`, `readEvents`, `sanitize`.
- Human Gates: `requestHumanGate`, `resolveGate`, `pendingGates`, `hasApprovedGate`.
- Loop: `transition`, `advance`, `recordGate`, `fail`, `block`, `resume`, `checkpoint`.
- Constantes de estados, gates, falhas e resultados.

## Restrições

- Envelopes são JSON válido armazenado com extensão `.yaml`.
- Telemetria é JSONL append-only.
- Checkpoints removem referências a secrets, tokens, passwords, credentials, `.env` e chain-of-thought.
- Aprovação e rejeição exigem identificador humano explícito; `agent`, `engine` e equivalentes são recusados.
- Specs `draft`, `review` ou `changed` bloqueiam implementação até aprovação `G-SPEC`.
- Gates desconhecidos são registrados como coverage gap, nunca como PASS.
- Falha externa bloqueia sem consumir tentativa.
- `run-gates` executa somente gates ativos declarados em `profiles/runrun-loop.yaml`, somente quando o envelope está em `gating` e sem Human Gates pendentes.
- Comandos e diretórios não são aceitos pela linha de comando: vêm exclusivamente do profile versionado; o diretório de execução não pode escapar da raiz do repositório.
- O resultado registra gate, duração, código de saída e diretório, mas nunca persiste stdout/stderr na telemetria.
- O motor não chama LLM/OpenCode, APIs, banco, migrations, deploy, PR/merge ou agentes. A execução automática limita-se aos product gates allowlisted.
