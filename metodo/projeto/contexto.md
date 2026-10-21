# Contexto do projeto

<!-- Preenchido no onboarding do método (v0.5.1, 2026-10-21). Fica curto: o detalhe vai para as skills do projeto. -->

## O que é

Prisma WorkSpace: plataforma open source de gestão ágil (Kanban, Scrum, horas, alocação, dashboards, wiki, portal externo e assistente de IA). Escopo do produto: múltiplos quadros, Kanban com arrastar e soltar, subtarefas, alocação de pessoas, anexos, contagem de horas, lead time por etapa, dashboards, Gantt, filtros salvos, ações em massa e automações. O próprio Prisma também é o rastreador do método de trabalho com agentes (`rastreador: prisma`, D100 a D102): ele rastreia a si mesmo.

## Para quem (cliente e usuários)

- **DETRAN-SE:** origem do produto e principal instalação em uso. A produção (`https://prisma.nordevs.com.br`) atende as equipes do DETRAN. A superfície do produto não traz menção institucional ao DETRAN (D68).
- **Community Edition:** distribuição gratuita, repositório público `antoniovitor10/prisma-workspace`. A licença ainda está pendente (skill `distribuicao-community`).
- **Cloud, Enterprise e serviços** (consultoria, treinamentos, suporte, customizações), pela Nordevs, financiam a evolução.

## Objetivo do negócio

Manter a produção do DETRAN estável enquanto o produto vira uma distribuição open source instalável por qualquer equipe, e servir de rastreador do método para os projetos do Vitor.

## Ambientes

| Ambiente | URL / local | Como publica |
|---|---|---|
| desenvolvimento (PC do Vitor) | `C:\Users\Vitor\Desktop\prisma`, `npm run dev` na raiz (porta 5450); planejamento e integração do Maestro | não publica |
| desenvolvimento (servidor Contabo, `169.58.61.117`) | `/home/agentes/projetos/prisma` só para `git fetch`; cada agente trabalha em `/home/agentes/projetos/prisma-wt/<agente>-<branch>`, com portas e projeto Docker próprios (skill `operacao-agentes`) | push só da branch da task |
| demonstração | projeto Docker `prisma-dev` (3100/3102) e `prisma-metodo` (3130 a 3139) no servidor | fora do alcance dos agentes |
| homologação | não existe (`tem_homologacao: false`) | |
| produção | `https://prisma.nordevs.com.br`, **na mesma máquina do servidor de desenvolvimento**: contêiner `prisma-workspace-api`, banco no contêiner `detran-kanban-db`, ponte de IA `prisma-ai-bridge` | merge na `main`: o CI testa, gera a imagem, roda o E2E e publica (skill `deploy`) |

## Stack resumida

.NET 8 (ASP.NET Core Web API, Clean Architecture, MediatR, FluentValidation, Serilog), EF Core 8 com SQL Server 2022, Identity e JWT; frontend React 19 com TypeScript, Vite e Styled Components; testes xUnit, Vitest e Playwright; CI no GitHub Actions. Detalhe e versões na skill `stack`.

## Roadmap curto

Fonte: `ROADMAP.md` (fica até o PR de remoção do legado; depois, este resumo e as HUs).

- **Concluídas:** fases 0 a 7 (fundação, domínio e banco, Kanban, tarefa rica, horas, inteligência, MVP para equipes de TI, portal externo), 9 (renovação visual D68/D70), 10 (visões e consultas por projeto), 11 (home autenticada), 12 (natureza e tipo de trabalho) e 13.1 (setup seguro da Community).
- **Em andamento:**
  - **Fase 16, método Git (D100 a D102):** a fase 1 (MCP `tools/prisma-mcp`) e o corte 2a (tokens com papel, webhook do GitHub, vínculo task-PR) estão em produção desde 2026-10-18, ligados só para o repositório `cooperarpay` (GitHub App `prisma-metodo-vitor`). Fases 2b (reconciliação) e 3 (gate `prisma/autorizacao`, deploy observado) não foram aprovadas. Os parâmetros do corte 2a (D102, agora ADR-0102) estão como **proposta**, à espera do Vitor.
  - **Fase 15, assistente de IA (D91):** fundação, conversas por provedor e modelo, ponte CLI; falta o teste humano da assinatura e do chat.
  - **Fase 14, programa v2:** lotes de domínio, sprint, identidade e histórico, módulos da Community, produtização e, por último, a consolidação das migrations. O que ainda está vivo vem do `backlog.md` como HUs abertas (ver Legado).
  - **Fase 13, distribuição open source:** licença, SBOM, SemVer, backup e restauração, ensaio de instalação limpa.
  - **Fase 8, aderência ao documento do DETRAN:** registro histórico; os itens abertos foram redistribuídos na fase 14.

## Próximo trabalho

- **HU-0001 Registrar a direção visual atual** (D68), aberta, landing fora. Extrai do código (`src/Prisma.Workspace.Web/src/styles/theme.ts`, `src/Prisma.Workspace.Web/DESIGN.md`) o `DESIGN.md` da raiz e cria a skill `design-system`. Nenhuma task de interface escolhe cor ou fonte antes dela.
- Trabalho vivo do `backlog.md`: HUs abertas, sem tasks, agrupadas por área; ganham tasks quando entrarem na fila.
- Criar o projeto `prisma-workspace` na organização Nordevs do Prisma, preencher `prisma.projeto_id` e instalar a GitHub App neste repositório.

## Autoridade

Em divergência, prevalece nesta ordem:

1. Vitor (decisão explícita na conversa ou na task)
2. `metodo/projeto/principios.md`
3. ADRs vigentes em `docs/decisoes/` (ADR-0001 a ADR-0102 vieram da `DECISIONS.md`, com o mesmo número)
4. HU e PRD aprovados em `docs/trabalho/`
5. Specs aprovadas antigas, citadas como referência pela skill `dominio`
6. Skills do projeto (`metodo/projeto/skills/`)

Uma HU que contradiz uma spec antiga aprovada precisa de ADR que diga o que muda.

## Glossário

| Termo | Significado |
|---|---|
| organização | tenant raiz; todo dado pertence a uma (D58) |
| projeto, quadro, coluna (etapa, `Stage`) | o projeto agrupa quadros; cada quadro tem colunas próprias (D89); a coluna define o status visível (D65) |
| tarefa (`WorkItem`) | item de trabalho; o nome evita colidir com `System.Threading.Tasks.Task` |
| natureza do trabalho | classificação do projeto e da tarefa (fase 12) |
| portal externo | formulário público para pedidos de fora da organização (fase 7) |
| ponte de IA | `infra/ai-bridge`, contêiner que fala com as CLIs de IA por assinatura |
| método Git | integração do método com o GitHub: tokens com papel, webhook, vínculo task-PR (D100 a D102) |
| G-SPEC, G-MIGRATION, G-WORKFLOW, G-HISTORY, G-SCOPE, G-DEPLOY, G-COMPLETION | nomes antigos dos portões humanos; o equivalente no método está na skill `areas-sensiveis` |
| D1 a D102 | decisões antigas da `DECISIONS.md`; hoje ADR-0001 a ADR-0102 |

## Pessoas

| Quem | Papel | Decide sobre |
|---|---|---|
| Vitor | dono do produto e desenvolvedor responsável | tudo; autoriza tasks, migrations, workflow, histórico, deploy e risco alto |
| Maestro (Claude Code, PC) | arquiteto e planejador; integra na `main` com o revisor aprovando e o CI verde | integração das tasks de risco normal (autonomia 2) |
| Agulha (agy), Linha (Claude Code) | implementador e testador, no servidor; quem testa nunca é quem implementou | nada sozinhos |
| Esquadro (Codex) | revisor de código e de segurança | aprovação técnica da revisão |

Time e papéis registrados no ADR-0104; operação na skill `operacao-agentes`.

## Legado

Até **2026-10-21** o projeto usou um método próprio: **AI-Native V0 + SDD (D67)**. Fluxo: história em `stories/` → spec gerada pela IA → G-SPEC → tarefas no `backlog.md` → contexto por `context/index.yaml` → implementação → testes → homologação da história. Contratos de agentes em `agents/`, grafo em `workflows/feature.yaml`, perfil em `profiles/prisma-loop.yaml`, motor em `tools/agent-loop`, governança em `tools/context-explorer` (job Context governance do CI) com painel de aprovação de gates.

A partir de 2026-10-21 vale o método (ADR-0103, que substitui D67, D78 e a parte de processo de D96 e D97). O que virou o quê:

| Antes | Agora |
|---|---|
| história + spec + G-SPEC (D67) | HU com critérios `CA-n`, `clarificar`, PRD na trilha épico, `analisar-consistencia` e autorização da task pelo Vitor no Prisma |
| `specs/` (48 aprovadas) | referências da skill `dominio`; as 5 superseded não migram |
| `backlog.md` | feitas e sucedidas (auditoria do PR #29, branch `docs/backlog-audit-20261020`) não migram; as vivas viram HUs abertas, sem tasks, por área |
| `stories/` | não migram; histórico |
| `DECISIONS.md` (D1 a D102) | ADRs com o mesmo número (D5 = ADR-0005); D102 entra como proposta |
| `agents/`, `workflows/`, `profiles/`, `tools/agent-loop` | papéis e `fluxo.md` do método |
| `context/index.yaml` | mapa domínio, código e testes da skill `dominio` |
| human gates G-* | princípios 1, 2, 3, 6 e 13 e a skill `areas-sensiveis` |
| `PROGRESS.md` e `NOTAS.md` | deixam de existir; o registro é o relatório da task e os comentários no Prisma |
| `AGENTS.md` antigo | `AGENTS.md` roteador do método; o conteúdo vivo foi para este arquivo, os princípios e as skills |
| time Tear, Prumo e Farol (D96, D97), Batuta e ai-memory | time de 2026-10-21 (ADR-0104), sem Batuta nem ai-memory para os agentes do servidor |

Os artefatos antigos continuam no repositório até um PR separado de remoção, para o CI seguir verde: o job Context governance exige as contagens atuais (skill `testes`). Até lá, nenhum deles autoriza nada e não se edita nenhum. Depois da remoção, o histórico (`PROGRESS.md`, `NOTAS.md`, `stories/`, `backlog.md`, specs) continua no git.
