---
id: ADR-0104
titulo: "Reconstrução do time de agentes de 2026-10-21 (Maestro, Agulha, Linha e Esquadro)"
status: aceita
data: 2026-10-21
origem: "onboarding"
substitui: "ADR-0096, ADR-0097"
---

# ADR-0104 · Reconstrução do time de agentes de 2026-10-21 (Maestro, Agulha, Linha e Esquadro)

## Contexto

O arranjo anterior de agentes (D96 e D97: Maestro no PC, Tear e Prumo no servidor Contabo e Farol no PC) concentrava a execução em um único agente no servidor e dependia do portal local do PC para QA visual. Com a adoção do método 0.5.1 e a exigência de independência estrita entre quem implementa, quem testa na interface e quem revisa código e segurança, o time foi reconstruído em 2026-10-21.

## Decisão

Definir a composição e a divisão de responsabilidades e portas do time fixo do Prisma WorkSpace a partir de 2026-10-21:

1. **Maestro (Claude Code, no PC do Vitor):**
   - Planeja, despacha tarefas, revisa o fechamento, integra na `main` (conforme autonomia configurada em `metodo/projeto/config.yaml`) e publica.
   - Centraliza toda a comunicação; agentes no servidor não invocam outros agentes diretamente.
2. **Agulha (`agy`, no servidor Contabo) e Linha (Claude Code, no servidor Contabo):**
   - Atuam como implementador (`metodo/nucleo/papeis/implementador.md`) e testador/QA (`metodo/nucleo/papeis/testador.md`) em rodízio cruzado: **quem testa ou revisa nunca é quem implementou**.
   - Agulha utiliza as portas `3140` a `3149` e o projeto Docker `prisma-agulha`.
   - Linha utiliza faixa de portas e projeto Docker próprios, isolados de Agulha, de `prisma-dev` (`3100`/`3102`) e de `prisma-metodo` (`3130`-`3139`).
   - Trabalham sempre em worktrees isoladas em `/home/agentes/projetos/prisma-wt/<agente>-<branch>`, mantendo `/home/agentes/projetos/prisma` apenas para `git fetch`.
3. **Esquadro (Codex, no servidor Contabo, único terminal Codex):**
   - Atua como revisor de código (`metodo/nucleo/papeis/revisor-codigo.md`) e revisor de segurança (`metodo/nucleo/papeis/revisor-seguranca.md`), sempre independente de quem implementou.
4. **Isolamento de produção e recursos compartilhados no servidor Contabo:**
   - É proibido tocar nos containers de produção (`prisma-workspace-api`, `prisma-ai-bridge`, `detran-kanban-*`, `prisma-workspace-api-rollback-*`), em `/home/dev`, `/opt`, Caddy, firewall, cron, SSH, `scripts/deploy-production.py` ou `method-git.env`.
   - Todo comando de Playwright ou navegador executa sob `flock /home/agentes/projetos/.trava-navegador <comando>`. Antes de execução pesada, verifica-se `uptime` (aguardando caso a carga de 1 minuto esteja acima de 14).

Esta decisão substitui a composição de papéis e terminais definida em D96 (`ADR-0096`) e D97 (`ADR-0097`).

## Alternativas descartadas

- **Manter um único executor (Tear) e QA no PC (Farol):** descartada por impedir paralelismo de execução no servidor e depender da máquina local para testes E2E e visuais.
- **Permitir que o mesmo agente faça implementação e QA da própria task:** descartada por violar o princípio de verificação independente do método 0.5.1.

## Consequências

### Positivas

- Paralelismo entre duas frentes de implementação/QA no servidor (Agulha e Linha) com isolamento de portas, containers e worktrees.
- Independência garantida entre implementação, QA visual/E2E (1440x900 e 390x844) e revisão de código/segurança (Esquadro).

### Negativas / custos

- Exige disciplina no uso de faixas de portas exclusivas, limpeza nominal de containers/volumes/bancos após os testes e uso obrigatório da trava de navegador (`flock`).

## Como reverter

Atualizar a configuração do time nos adaptadores do projeto e registrar nova ADR redefinindo os agentes e faixas de portas.
