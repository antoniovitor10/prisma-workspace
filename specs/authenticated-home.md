# SPEC-AUTHENTICATED-HOME: Página inicial depois do login

**Status:** approved

**Aprovação humana:** PO determinou a mudança em 2026-09-03 e já havia autorizado o Codex a definir o formato e seguir
dentro da refatoração visual Prisma.

## Objective

Substituir o alias `/home → Projetos` por uma home autenticada útil, intuitiva e visualmente marcante, construída com
dados reais já autorizados e os padrões da D68/D70.

## Scope

- `/home` torna-se a entrada padrão para pessoas com acesso ao workspace.
- Cabeçalho de boas-vindas com proposta operacional e ações para Meu trabalho e Projetos.
- Resumo real de tarefas atribuídas, de hoje, atrasadas e bloqueadas.
- Lista priorizada por atraso, bloqueio, prazo do dia e prioridade.
- Continuidade por projetos recentes e atalhos para Projetos, Relatórios, Solicitações e Meu trabalho.
- Item `Início` na navegação desktop/mobile e marca Prisma apontando para a home.
- Papel de portal externo (`role 8`) permanece direcionado a Solicitações.

## Out of Scope

- Novos endpoints, tabelas, métricas, preferências persistentes ou widgets configuráveis.
- Alterar regras de prioridade, autorização, workflow ou dados de projetos/tarefas.
- Executar E2E contra produção.

## Functional Requirements

1. Toda informação numérica vem de `getMyWork`; projetos vêm de `getProjects(false)`.
2. Falha parcial de dados mantém atalhos navegáveis e informa indisponibilidade sem skeleton infinito.
3. A lista principal ordena itens abertos por atraso, bloqueio, hoje e prioridade, limitada a seis itens.
4. Cards de resumo levam para Meu trabalho; projetos recentes levam ao backlog do projeto.
5. Layout é responsivo, light/dark e preserva touch targets mínimos de 44 px.
6. A rota raiz redireciona pessoas do workspace a `/home`; papel 8 continua em `/requests`.

## Acceptance Criteria

- Após autenticação comum, `/` redireciona para `/home` e exibe a mensagem “O trabalho que importa”.
- A navegação marca `Início` como ativa e mantém os destinos existentes.
- Resumo, prioridade e projetos usam respostas reais das APIs existentes.
- Em desktop e mobile não há overflow e as ações principais permanecem acessíveis.
- Build, Vitest, lint e Playwright E2E passam.

## Data / API / Authorization Impact

Nenhum impacto de schema ou contrato. Somente composição no React usando endpoints existentes e redirecionamento por
papel já disponível no frontend.

## Human Gates

- `G-SCOPE`: aprovado pela determinação direta do PO em 2026-09-03.
- `G-SPEC`: aprovado pela determinação de execução e delegação explícita do formato em 2026-09-03.
- `G-MIGRATION`: não aplicável.
- `G-DEPLOY`: aprovado pela instrução explícita do PO para publicar em produção em 2026-09-03.

## Traceability

US-HOME-001 → SPEC-AUTHENTICATED-HOME → TASK-039 → Vitest/Playwright → homologação visual

## Resultado da implementação

Implementada em 2026-09-03 com entrada real em `/home`, dados de Meu trabalho e Projetos, navegação superior/mobile,
estados responsivos e preservação do redirecionamento do portal externo. Build, lint, Vitest e Playwright desktop/mobile
foram aprovados; a extensão do Codex no Chrome não está exposta a esta sessão para inspeção visual interativa.
