# ROADMAP - fases do projeto

Objetivo do produto: construir o **Prisma WorkSpace**, plataforma open source
integrada para demandas internas e solicitacoes externas (projetos, Scrum, Kanban,
horas, alocacao, lead time, dashboards, Gantt, filtros, acoes em massa e
automacoes), com edicoes Community/Cloud/Enterprise e servicos de consultoria.

Marcacao: `[x]` feito, `[~]` em andamento, `[ ]` pendente.

Toda IA consulta isto para saber o escopo da fase atual. **Nao inventar escopo
fora daqui**. Se a fase nao esta descrita, pare e pergunte ao PO.

## [x] Fase 0 - Fundacao

Solution Clean Architecture, SQL Server, EF Core, auth JWT + Identity,
`/health`, Swagger.

## [x] Fase 1 - Dominio & Banco

Entidades + migration inicial: `Board`, `Stage`, `WorkItem`, `TimeEntry`,
`StageHistory`.

Repositorios + CQRS base de Stage e WorkItem. `MoveWorkItem` grava
`StageHistory` (D5).

Confirmado no codigo atual:
- `WorkItem.Subtitle` entrou.
- `WorkItem.ParentId` entrou (D4).
- A tabela/entidade `TimeEntry` entrou.

## [x] Fase 2 - Kanban core + Frontend

Multiplos quadros, etapas, CRUD de card, mover card, titulo/subtitulo.

Frontend React SPA + Styled Components (D7) integrado a API.

Encerrada por decisao do PO em 2026-06-30 para avancar a Fase 3.

Observacoes de transicao:
- Login e emissao de JWT real corrigidos.
- Seletor de modo claro/escuro implementado no frontend.
- Persistencia real do timer permanece no escopo da Fase 4.
- Isolamento por usuario deve ser tratado como hardening antes de considerar
  Fase 3 concluida.

## [x] Fase 3 - Tarefa rica

- **Alocacao:** tabela de juncao `WorkItemAssignee`
  (`WorkItemId` + `UserId`). Endpoints para listar usuarios atribuiveis e
  atribuir/remover. N usuarios por card.
- **Subtarefas:** criar `WorkItem` filho via `ParentId` (D4). Listar subtarefas
  dentro do card pai. No board so aparecem cards com `ParentId` nulo.
- **Anexos:** entidade `Attachment`; upload/download por card. Decidir storage
  (FILESTREAM no SQL Server vs disco/blob) e **registrar em DECISIONS.md**.
- **Opcional:** comentarios por card.

Concluida em 2026-06-30:
- Responsaveis por card no backend e no modal do card.
- Subtarefas criadas via `ParentId`, listadas no card pai e ocultas do board.
- Upload/download de anexos com storage local conforme D8.
- Comentarios nao implementados por serem opcionais.

## [x] Fase 4 - Contagem de horas

- `TimeEntry`: timer start/stop (`EndedAt` nulo = rodando) + lancamento manual.
- Agregacoes: total por card, por usuario e por board.
- Conectar o Timer do frontend a esses endpoints (hoje pode estar so no estado
  do React).

Concluida em 2026-06-30:
- Endpoints de timer start/stop, timer em andamento e lancamento manual.
- Agregacoes por card, usuario e board.
- Frontend conectado aos endpoints reais de `TimeEntry`; timer deixou de ser
  apenas estado local do React.

## [x] Fase 5 - Inteligencia

- Lead time / tempo medio por etapa, a partir do `StageHistory`.
- Dashboards com metricas configuraveis.
- Visao Gantt.

Concluida em 2026-06-30:
- Calculo de tempo medio por coluna implementado no backend via CQRS (`GetBoardLeadTimeQuery`) e exposto na API (`/api/Boards/{id}/lead-time`).
- Modal de Lead Time e Dashboard integrado no frontend para exibir métricas consolidadas do board e distribuição de cards.
- Modal de cronograma Gantt com estimativa de cronograma baseada nas datas de criação e vencimento dos cartões.

## [x] Fase 6 - MVP integrado para equipes de TI

Redesenho do produto para combinar gestao de tempo e fluxo do Runrun.it,
organizacao e multiplas visoes do ClickUp e planejamento Scrum do Azure Boards.

### [x] 6A - Fundacao de produto e dados

- Hierarquia Organização -> Equipe -> Projeto -> Tarefa -> Subtarefa -> Checklist,
  com Épico -> História de usuário -> Tarefa -> Subtarefa nos projetos de desenvolvimento.
- Quadros, colunas, backlog e sprints como visões operacionais do projeto, sem criar
  níveis organizacionais adicionais.
- Papeis por projeto: ProjectAdmin, ProductOwner, ScrumMaster, Member e Viewer.
- Tipos Epic, Feature, User Story, Bug, Task e Subtask.
- Categorias semanticas de etapa e migracao compativel dos dados existentes.
- Sprints, meta, capacidade individual e backlog priorizado.

### [x] 6B - Experiencia Scrum e redesign

- Shell corporativo denso com sidebar, topbar e navegacao contextual do projeto.
- Product Backlog hierarquico e planejamento de sprint por drag-and-drop.
- Sprint Backlog explicito com epicos, historias, bugs, tarefas, story points,
  criterios de aceite, bloqueios e acesso ao detalhe lateral.
- Quadro da sprint, Meu Trabalho e detalhe rico da tarefa.
- Velocity, burndown, capacidade e progresso da meta.
- Ciclo Planejada -> Ativa -> Concluida ou Cancelada, uma ativa por projeto, escopo terminal
  fotografado e historico navegavel mesmo quando pendencias voltam ao backlog ou seguem para outra sprint.

Concluida em 2026-07-15:
- Menu recolhivel, navegacao principal e pesquisa global com `Ctrl + K`.
- Criacao rapida exigindo somente o titulo e detalhe em painel lateral acessivel.
- Backlog hierarquico com reordenacao e planejamento de sprint por drag-and-drop.
- Dashboard da sprint com meta, KPIs, burndown, velocity, capacidade e quadro da sprint.
- Divisao do frontend por rotas para manter o carregamento inicial abaixo do limite de 500 kB.

### [x] 6C - Organizacoes, acesso e equipes

- Organizacoes, preferencias, membros, convites e administradores.
- Contexto ativo e isolamento por `OrganizationId` com filtros globais.
- Perfis iniciais e permissoes por organizacao/recurso.
- Equipes com edicao, desativacao, lider e capacidade individual/padrao.

Concluida em 2026-07-15:
- Tenant ativo obrigatório via `X-Organization-Id`, associação ativa e filtros globais do EF Core.
- Cadastro e preferências da organização, seletor no shell, membros, convites por link e proteção do último administrador.
- Dez perfis iniciais e matriz híbrida de permissões, com concessões/negações por sete escopos.
- Equipes com edição, desativação lógica, líder, vínculo com projetos e capacidades padrão/individual.
- Migration compatível com dados legados, testes de isolamento em memória e documentação do header no OpenAPI.

### [x] 6D - Projetos e tarefas completos

- Ciclo de vida do projeto: editar, arquivar, reativar, status, metodologia, datas e responsavel.
- Membros, equipes, etiquetas, configuracoes, campos personalizados e historico do projeto.
- Tarefa rica com identificador estavel, origem, solicitante, responsavel principal e participantes.
- Edicao completa, duplicacao, arquivamento, criterios de aceite, dependencias e bloqueios.
- Seguidores, comentarios, anexos, checklist, subtarefas, campos personalizados e historico.
- Painel lateral editavel com feedback otimista e preservacao da tela de origem.

Concluida em 2026-07-15:
- Projetos com metodologia, status, datas, responsavel, arquivamento logico, membros, equipes, etiquetas, campos personalizados e historico administrativo.
- Tarefas com numero sequencial estavel, origem/solicitante, responsavel e participantes, datas, horas, story points, criterios de aceite, seguidores e arquivamento logico.
- Duplicacao, dependencias, bloqueios, relacionamentos, valores de campos personalizados e auditoria integrados ao mesmo `WorkItem`.
- Painel lateral editavel e otimista reunindo classificacao, checklist, subtarefas, anexos, comentarios, historico e acoes de ciclo de vida.
- Migration `20260715212227_Fase6D_Projects_WorkItems` aplicada e validada no SQL Server local com jornada real da API.

### [x] 6E - Fluxos, Meu Trabalho e produtividade

- Status configuraveis por projeto, cores, ordem, estado inicial/final e matriz de transicoes.
- Associacao entre status e colunas, validacao de transicao e limite de WIP no backend.
- Meu Trabalho consolidado: atribuicoes, autoria, acompanhamento, prazos, bloqueios, mencoes,
  solicitacoes externas, aprovacoes e alertas importantes.
- Kanban com filtros completos, agrupamento, ordenacao, preferencias de cartao e atualizacao SignalR.
- Filtros pessoais salvos.
- Acoes em massa: mover, atribuir, priorizar, etiquetar e incluir em sprint.
- Automacoes v1 auditadas e protegidas contra loops.

Entregue em 2026-07-16 para as funcionalidades 7, 8, 9 e 10:
- Workflow por projeto com status, cores, ordem, estado inicial/final, matriz de transicoes,
  associacao com colunas e migration/backfill idempotente.
- Transicoes e WIP validados no backend em criacao, edicao, drag-and-drop e acoes em massa.
- Meu Trabalho unificado com atribuicoes, autoria, acompanhamento, hoje/semana, atrasos,
  bloqueios, solicitacoes externas, mencoes, aprovacoes, prazos e alertas derivados.
- Kanban com filtros completos e salvos, agrupamento, ordenacao, cartoes configuraveis e
  atualizacao autenticada por SignalR.
- Backlog operacional com criacao rapida, prioridade por drag-and-drop, filtros estruturados,
  agrupamentos, edicao inline de titulo/prioridade/story points, relacao com epicos, selecao
  multipla, planejamento backlog-sprint e indicadores de dependencias/bloqueios.

Concluída em 2026-07-20:
- seleção visual e ações em massa no Kanban para mover, atribuir/desatribuir, priorizar,
  etiquetar e planejar em sprint, com limite de 200 itens e validação transacional;
- editor completo de automações básicas com criação, edição, pausa, exclusão e alvos tipados;
- validação de workflow, WIP, usuários, etiquetas, sprints, duplicidade e ciclos tanto na
  configuração quanto na execução, com limite defensivo, eventos e auditoria;
- autorização por organização/projeto/quadro/tarefa aplicada também aos endpoints legados.

### [x] 6F - Homologação técnica do MVP

- Autorizacao e isolamento por projeto em todas as consultas e mutacoes.
- Testes unitarios, integracao e fluxos ponta a ponta.
- Migrations aplicadas em SQL Server e smoke tests autenticados dos fluxos críticos.
- Inicializador local reproduzível, documentação de diagnóstico, segurança e checklist de aceite.

O piloto com uma equipe real e a liberação gradual permanecem como atividades operacionais de
implantação, pois dependem de usuários, SMTP e cofre de segredos institucionais.

## [x] Fase 7 - Portal externo e módulos transversais do MVP

- Portal externo, protocolo, acompanhamento e fila de triagem conforme D16.
- Conversao vinculada da solicitacao externa em item de trabalho interno.
- Notificacoes e formularios de entrada.
- Campos personalizados, relatórios e dashboards configuráveis.
- SLA, apontamento de horas, pesquisa global, auditoria e segurança.

Nucleo do Portal Externo antecipado em 2026-07-20:
- configuracao por projeto/quadro, publicacao por slug e escolha entre link, login, convite e codigo;
- abertura como `WorkItem` de origem externa, protocolo e chave segura de acompanhamento;
- consulta de status, respostas publicas, anexos, avaliacao e confirmacao da conclusao;
- fila interna real de triagem e resposta, preservando a mesma tarefa operada no Kanban.

Entregue em 2026-07-20 para as funcionalidades 15, 16 e 17:
- formulários múltiplos por portal com título, descrição, categoria, mensagem de confirmação,
  campos padrão/personalizados, obrigatoriedade, opções, regex e condição simples;
- configuração de fila, prioridade, equipe, responsável, regras condicionais de atribuição,
  extensões/MIME, quantidade e tamanho de anexos;
- proteção pública com rate limiting por IP/rota, honeypot e tempo mínimo de preenchimento;
- submissão multipart que cria protocolo, `ExternalRequest`, `WorkItem`, valores, campos internos,
  anexos e evento inicial, preservando uma única tarefa em Kanban, lista, backlog, relatórios,
  dashboard e Meu Trabalho conforme seus filtros normais;
- confirmação por e-mail com fallback seguro quando SMTP não estiver configurado;
- fila de triagem com aceitar, recusar com justificativa, pedir informação e notificar, alterar
  categoria/prioridade/responsável/equipe/projeto, enviar a backlog/Kanban, marcar duplicidade e
  vincular tarefa, sempre com evento append-only também refletido no histórico do `WorkItem`.

Entregue em 2026-07-20 para as funcionalidades 18 a 24:
- conversa pública separada de comentários internos, DTO mínimo no acompanhamento por protocolo,
  anexos explicitamente públicos e notificação por e-mail das respostas da equipe;
- SLA configurável por projeto com calendário, dias úteis, feriados, regras por categoria/prioridade,
  snapshot por solicitação, pausa aguardando o solicitante, alertas e estados operacionais;
- cronômetro e apontamento manual no painel da tarefa, histórico e relatórios de horas por usuário,
  equipe e projeto, sem qualquer dado financeiro;
- campos personalizados ampliados para texto longo, percentual, data/hora, usuário, equipe e URL,
  com validação e uso em tarefas, formulários, filtros e relatórios;
- relatórios prontos de tarefas, solicitações, SLA, horas, sprints, burndown e volume por período;
- construtor declarativo com oito fontes fechadas, colunas, métricas, filtros, agrupamento,
  ordenação, período, sete visualizações, salvamento, duplicação, compartilhamento e CSV;
- dashboards de colaborador, gestor e projeto com filtros básicos, KPIs, séries e itens prioritários.

Entregue em 2026-07-20 para as funcionalidades 25 a 30 e requisitos transversais 37 a 41:
- central persistente de notificações, leitura, preferências por evento/canal, entrega de e-mail e
  lembretes deduplicados de prazo e SLA;
- pesquisa global autorizada de tarefas, solicitações, projetos, usuários, equipes e sprints,
  acessível por `Ctrl + K`, com catálogo fechado de comandos rápidos;
- auditoria imutável e multitenant com diferenças anterior/novo, origem, IP e correlação;
- hardening de Identity/JWT com refresh token rotativo, confirmação de e-mail, recuperação de
  senha, lockout, políticas, rate limiting, cabeçalhos de segurança e segredos externos;
- erros padronizados, cobertura FluentValidation, catálogo OpenAPI por módulo e testes dos fluxos
  transversais, preservando MediatR, EF Core e a estrutura por features do React.

Fechamento do MVP em 2026-07-20:
- ações em massa, automações básicas e matriz de autorização concluídas e homologadas;
- visão da solução, módulos, dependências, estruturas .NET/React, modelo inicial de dados,
  roadmap, backlog, regras, casos de uso, endpoints, componentes, critérios e testes
  consolidados em `docs/MVP-ESPECIFICACAO.md`;
- execução, segurança, matriz de testes e homologação documentadas em
  `docs/MVP-HOMOLOGACAO.md` e `README.md`.

Mural, releases, templates, integrações, roadmap/Gantt avançados e automações avançadas ficam
como evolução futura fora do MVP, conforme D37; não são pendências desta entrega.

## [~] Fase 8 - Aderência ao documento operacional do Detran

> **Reconciliação 2026-09-08:** os itens ainda abertos desta fase foram auditados contra o código e
> redistribuídos nos lotes da **Fase 14**, com id de tarefa, dependências, gates e testes em `backlog.md`.
> Nada foi descartado: D52, D55, D56, D57, D58, D59, D61, D62, D63 e D65 continuam vigentes e cada uma tem
> tarefa correspondente. Esta fase permanece `[~]` como registro histórico e deixa de ser a fila de execução.

Fase autorizada por PO em 2026-08-20 para concluir os requisitos auditados em
`docs/ProjetoRunrun-Detran.docx.pdf`, preservando o comportamento atual quando o documento não define
uma mudança.

- Nome funcional de responsáveis e participantes, sem usar e-mail como identificação principal.
- Story Points ocultos em todas as superfícies de projetos Kanban.
- Detalhe único da tarefa com seis abas e separação entre comentários, histórico automático e Linha do tempo.
- Histórico estruturado antes/depois e Linha do tempo textual das movimentações entre colunas conforme D61.
- Product Backlog hierárquico com subtarefas, expansão e colapso conforme D36.
- Ocultar dependências/pré-requisitos conforme D59, preservando temporariamente dados e estruturas internas, mas
  removendo sua exposição e qualquer efeito sobre operação, automações, filtros, indicadores e relatórios.
- Quadro da Sprint com drag-and-drop persistente e validações reais de workflow/WIP.
- Status visível e operacional deve convergir para a coluna atual conforme D65. Templates, transições e
  `WorkflowStatusId` por projeto permanecem somente como legado técnico a ocultar/migrar com segurança.
- Adequar quadros ao modelo operacional transversal da D52: Kanban inicial, seletor superior, posição singular,
  colunas livres abertas/concluídas, acesso por usuário/equipe, filtros Runrun.it e arquivamento conjunto.
- Adequar filtros à D64: remover compartilhamento, permitir escopo pessoal por quadro ou global e sinalizar
  critérios globais ignorados por incompatibilidade sem bloquear os demais.
- Implementar a D62 na gestão de colunas: confirmação de impacto, conclusão atômica das tarefas e descendência
  recursiva na mudança aberta -> concluída, reabertura atômica no sentido inverso e histórico por tarefa.
- Adequar autorização à D55: cinco perfis-base, perfis personalizados, remoção da hierarquia fixa duplicada por
  projeto, escopo de quadro e acesso derivado Quadro -> Projetos, preservando deny explícito e menor privilégio.
- Ocultar a chave técnica de projeto em toda a experiência, mantendo geração exclusiva no backend e os usos
  internos necessários para unicidade, importação, busca técnica, compatibilidade e integrações conforme D56.
- Adequar a gestão de projetos à D57: criação apenas com nome/descrição e sem quadro automático, estados Ativo e
  Arquivado, gestão por Administrador/Gestor, múltiplas equipes/membros com acesso herdado e arquivamento
  recuperável do projeto e de suas tarefas sem alterar os quadros transversais independentes da D52.
- Preservar o visual atual da lista de projetos conforme D66; a proposta estética da
  `SPEC-PROJECTS-VISUAL-REFRESH` e a TASK-033 foram canceladas e não representam pendência da fase.
- Consolidar organização como tenant raiz conforme D58: usuário comum em um único tenant sem seletor; seleção
  entre organizações e ciclo criar/editar/arquivar/restaurar reservados ao Administrador da plataforma, com
  isolamento integral e arquivadas somente leitura.
- Cobertura .NET, Vitest/Testing Library e Playwright E2E obrigatória conforme D39/D40.
- Operação AI-Native story-first conforme D67: história registrada antes da spec; IA gera/revisa o contrato; `G-SPEC`
  aprova somente a spec; observações da homologação geram tarefas rastreáveis para análise, implementação e nova validação.

## [x] Fase 9 - Renovação visual sistêmica Prisma

Fase autorizada pelo PO em 2026-09-03, com `G-SPEC` da `SPEC-PRISMA-VISUAL-SYSTEM` aprovado na mesma data.
Concluída em 2026-09-03 com validação visual iterativa e gates automatizados aprovados.

- Estabelecer baseline visual e E2E do rebrand D68 já implantado.
- Evoluir fundação visual e login responsivo.
- Refinar shell superior sem reintroduzir sidebar global.
- Unificar cabeçalhos, toolbars, superfícies, cards, métricas, estados vazios e skeletons.
- Aplicar a linguagem às rotas principais em fatias verificáveis: Kanban, Meu trabalho, Projetos, Relatórios e Configurações.
- Validar modos claro/escuro, contraste, teclado, movimento reduzido e viewports desktop/mobile.
- Manter regras, contratos, dados e permissões inalterados; expansão funcional permanece nas specs próprias.

## [x] Fase 10 - Visões segmentadas e consultas por projeto

Escopo solicitado e aprovado pelo PO em 2026-09-03. Primeira entrega definida pela D71, publicada e validada
visualmente no Chrome/Codex em 2026-09-04, incluindo a correção do card de segmento selecionado.

- Segmentar itens do projeto em Épicos, Bugs, Features, Product Backlog Items e Tarefas.
- Exibir propriedades úteis e contagens por tipo sem alterar a hierarquia canônica.
- Compor consultas no contexto do projeto, preservando autorização e filtros pessoais.
- Usar compositor visual com consultas pessoais salvas e URL serializável; linguagem textual e API/exportação ficam fora da primeira entrega.
- Aplicar padrões de usabilidade do ClickUp sem copiar sua identidade visual.

## [x] Fase 11 - Home autenticada Prisma

Escopo definido diretamente pelo PO em 2026-09-03 e contratado na `SPEC-AUTHENTICATED-HOME`.
Concluída em 2026-09-03 com validação automatizada desktop/mobile.

- Transformar `/home` em visão geral real, removendo o alias para Projetos.
- Apresentar prioridades pessoais, resumo acionável e projetos recentes.
- Adicionar `Início` à navegação superior e mobile.
- Preservar a entrada de usuários de portal externo em Solicitações.
- Validar responsividade, navegação, estados parciais e E2E desktop/mobile.

## [x] Fase 12 - Natureza e tipo de trabalho do projeto

Escopo aprovado pelo PO em 2026-09-04 na `SPEC-WORK-NATURE`, implementado e publicado na mesma data.

- Classificar a criação como Projeto, Melhoria ou Sustentação.
- Classificar também por um dos nove Tipos de Trabalho predefinidos.
- Persistir a classificação no `Project`, sem criar entidade central de Demanda e preservando Solicitações.
- Tratar Projeto como iniciativa temporária; Melhoria como evolução incremental; Sustentação como operação e suporte.
- Manter subdivisões e etapas internas configuráveis, sem geração ou recomendação por IA.
- Definir compatibilidade dos registros existentes antes da migration.
- Aplicar migration segura, manter legados como Não classificado e validar criação/edição/filtros em produção.

## [~] Fase 13 - Produto e distribuição open source

Escopo e plano aprovados pelo PO em 2026-09-05 na `SPEC-OPEN-SOURCE-DISTRIBUTION`. A preparação privada pode
prosseguir; tornar o repositório público permanece bloqueado até escolha de licença, auditoria final e `G-DEPLOY`.

> **Reconciliação 2026-09-08:** o escopo remanescente desta fase (licença, SBOM, proveniência, SemVer,
> backup/restauração, passivo npm/NuGet e ensaio de instalação limpa) passa a ser executado pelo lote
> `productization` da **Fase 14**, como `TASK-500` a `TASK-508`. A consolidação de migrations sai desta fase e
> vira o lote final `migrations-consolidation` (`TASK-600` a `TASK-603`), porque a base real de produção exige
> cadeia incremental durante todo o programa (D80).

- Preservar o repositório atual como arquivo privado e criar checkpoint recuperável.
- Criar `antoniovitor10/prisma-workspace` privado com histórico novo e snapshot por allowlist.
- Remover conteúdo institucional, dados reais, artefatos gerados e configuração específica de infraestrutura.
- Migrar nomenclatura técnica legada em lotes cobertos por build, testes e E2E.
- Entregar Compose independente, onboarding seguro, backup/restauração e instalação limpa.
- Modernizar runtimes e documentar uso licenciado do SQL Server.
- Criar documentação comunitária, CI de segurança, SBOM, proveniência e releases SemVer.
- Ensaiar instalação e upgrade em ambiente limpo antes de `v0.1.0` público.

### 13.1 Setup inicial seguro da Community (concluído em 2026-09-08)

Derivado da `SPEC-OPEN-SOURCE-DISTRIBUTION` em `SPEC-INSTALLATION-SETUP` (`approved`). Uma instalação normal
começar sem usuários ou dados demo e permitir criar, uma única vez, a primeira organização e seu Administrator com
token externo e transação atômica. `Setup:Enabled` precisa ser explícito; um singleton persistido impede reabertura após
a primeira conclusão. O seed demo será Development-only, opt-in e neutro, separado do setup real e sem mutar instalações
existentes. UI guiada de primeiro acesso, testes unitários e E2E desktop/mobile foram entregues. A concorrência do
setup foi validada contra SQL Server real. `G-SPEC` e `G-MIGRATION` foram aprovados diretamente pelo PO em 2026-09-05.

## [~] Fase 14 - Programa Prisma WorkSpace v2

Recorte aprovado em 2026-09-21: descrição rica e ampla da tarefa, conforme `specs/rich-task-description.md`, sem edição com IA e sem migration.

Recorte aprovado em 2026-09-19: onboarding direto pelo convite (`specs/invitation-onboarding.md`), nome completo, senha com confirmação, aceite e sessão automáticos; conta existente autentica sem redefinir senha. G-SPEC e G-DEPLOY autorizados diretamente pelo PO. Sem migration.

Fase autorizada pelo PO em 2026-09-08 e registrada na D80. O PO determinou que todo o trabalho passa a ocorrer
no repositorio `prisma-workspace`, autorizou o deploy em producao sem restricao para
`https://prisma.nordevs.com.br` e aprovou em bloco as specs ativas **exceto** a `SPEC-S-003`, que voltou para
`draft` e recebeu o contrato novo de Sprint x Project N:N.

A auditoria de 2026-09-08 comparou as 40 specs de `specs/` com o codigo real e classificou cada uma com
evidencia de arquivo e linha. Resultado: 13 `implemented`, 15 `partially_implemented`, 4 `not_implemented`,
1 `blocked_by_gate` e 6 `superseded`. A tabela completa, as tarefas por lote, os arquivos centrais disputados e
as perguntas de gate estao em `backlog.md`.

Regra estrutural da fase: **a cadeia de migrations permanece incremental do inicio ao fim**. A hipotese de
migration inicial unica (fresh-install-only) e incompativel com a base real de producao, que possui dados e
historico de migrations aplicadas. A consolidacao acontece somente no lote final.

Lotes, na ordem recomendada de execucao:

- `TASK-BUG-001` isolado — corrigir o defeito relatado pelo PO: concluir no Kanban nao atualiza a tarefa.
  A causa principal auditada e a coluna nascer com `Stage.Category = InProgress`, somada as tres fontes de
  verdade concorrentes entre `Stage`, `WorkflowStatus` e `CompletedAt`.
- `core-domain-v2` — organizacoes, permissoes, quadros/colunas/WIP, status canonico, gestao de projetos,
  gestao de tarefas, criacao rapida, chave tecnica e metodologia oculta.
- `sprint-planning-v2` — sprints N:N, backlog, ordem visual do Kanban e filtros salvos.
  **Bloqueado pelo `G-SPEC` da `SPEC-S-003 v2`.**
- `identity-time-history-v2` — autenticacao, nome canonico, horas, historico, lead time, dependencias ocultas,
  notificacoes e anexos.
- `community-modules-v2` — equipes, wiki, portal externo, dashboards, SLA, acoes em massa, Gantt oculto,
  sistema visual, home autenticada, consultas por projeto e shell superior.
- `productization` — neutralizacao de residuos, vulnerabilidades npm/NuGet, documentacao de instalacao,
  backup e restauracao, SBOM, SemVer, licenca e o deploy da D80.
- `migrations-consolidation` — **por ultimo**, com ensaio em copia restaurada da base de producao.

Human Gates pendentes desta fase: `G-SPEC` da `SPEC-S-003 v2`; estrategia de backfill de `Stage.Category`;
mapeamento dos dez perfis para cinco; mapeamento de `ProjectStatus`; `G-SCOPE` da identidade do Administrador
da plataforma; escolha da licenca; apontamento em tarefa concluida; visibilidade da capacidade da sprint; e
`G-HISTORY` do `SprintItemSnapshot`. Nenhum deles pode ser aprovado por agente.
