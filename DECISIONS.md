# DECISIONS — decisões travadas (ADR-lite)

Uma linha por decisão. Pra mudar: não apague, marque ~~riscado~~ e adicione a nova abaixo.
Decisão que está aqui **não se re-discute** — qualquer IA respeita.

- **D1** — Backend em .NET 8 + Clean Architecture. (2026-06-30)
- **D2** — Banco SQL Server via Entity Framework Core. (2026-06-30)
- **D3** — Auth = JWT + ASP.NET Core Identity. (2026-06-30)
- **D4** — Subtarefa = a própria entidade `WorkItem` com `ParentId` (auto-referência),
  e não uma tabela separada. Assim subtarefa herda alocação, anexo e horas de graça. (2026-06-30)
- **D5** — Lead time é capturado **desde a Fase 2** gravando uma linha em `StageHistory`
  a cada movimentação de card — mesmo antes do dashboard existir. Os dados acumulam desde o dia 1. (2026-06-30)
- **D6** — ~~[EM ABERTO] Frontend: **Next.js + TS + Tailwind** (consome a API .NET, joga a favor da sua força em React) **vs Blazor Server** (tudo em C#, um deploy só, real-time via SignalR de brinde). Decidir na Fase 2.~~ (2026-06-30)
- **D7** — Frontend: **React.js (SPA) + Styled Components** (sem Tailwind). Consome a API .NET. Decidido pelo usuário na Fase 1. (2026-06-30)

- **D8** - Storage de anexos na Fase 3: arquivos ficam em disco local controlado pela API (`App_Data/attachments`) e metadados ficam no SQL Server via entidade `Attachment`. Evita configurar FILESTREAM agora e mantem caminho simples para trocar por blob storage futuramente. (2026-06-30)
- **D9** - ~~Identidade visual oficial adotada a partir do Manual do Governo de Sergipe: padrão light-first, sem gradientes, sem glow, com as cores oficiais (#164194, #008ECF, #76B82A, #FBBA00) e fontes Hanken Grotesk + Inter.~~ Sucedida pela D68. (2026-06-30)

- **D10** — ~~O produto passa a usar a hierarquia **Projeto → Times → Quadros → Itens de trabalho**. `Project`
  e `Board` são entidades distintas e um projeto pode conter vários quadros.~~ Parcialmente sucedida pela D52:
  as entidades continuam distintas, mas quadro deixa de ser filho funcional de projeto. (2026-07-15)
- **D11** — O MVP adota Scrum operacional: backlog hierárquico, sprints, meta, capacidade, velocity e burndown. Tipos oficiais: Epic, Feature, User Story, Bug, Task e Subtask. (2026-07-15)
- **D12** — Autorização por projeto com os papéis ProjectAdmin, ProductOwner, ScrumMaster, Member e Viewer; administradores globais continuam no ASP.NET Core Identity. (2026-07-15)
- **D13** — ~~O redesenho mantém React SPA + Styled Components e a identidade D9, com layout corporativo denso.~~ Mantém React SPA + Styled Components com layout denso; a identidade visual passa a seguir a D68. TanStack Query, dnd-kit e Recharts podem ser adicionados para estado remoto, interações e gráficos. (2026-07-15)
- **D14** — Azure DevOps é referência funcional; sincronização com repositórios e pipelines fica fora do MVP. (2026-07-15)
- **D15** — O toolkit do frontend da Fase 6B será composto por **TanStack Query** para estado remoto,
  **React Hook Form + Zod** para formulários tipados, **Radix Dialog** para superfícies acessíveis,
  **dnd-kit** para planejamento visual, **Recharts** para indicadores e **Vitest + Testing Library**
  para testes. Styled Components continua sendo a única camada de estilos. (2026-07-15)
- **D16** — Solicitação externa não será um fluxo isolado: ela terá protocolo e dados do
  solicitante, mas será vinculada ao mesmo `WorkItem` operado no backlog, sprint, lista e Kanban.
  A origem externa deve permanecer identificável durante todo o ciclo. (2026-07-15)
- **D17** — O SaaS será multitenant por **organização**. Agregados-raiz recebem
  `OrganizationId` obrigatório; entidades-filhas herdam o tenant pelo agregado. A organização ativa
  é enviada em `X-Organization-Id`, validada contra uma associação ativa e aplicada por filtros
  globais do EF Core. Ausência de contexto não libera leitura cruzada. (2026-07-15)
- **D18** — Autorização adota modelo híbrido: perfil-base por organização mais concessões
  explícitas por escopo (organização, equipe, projeto, tarefa, relatório, formulário e solicitação).
  Administrador da organização possui todas as permissões; uma negação explícita prevalece
  sobre concessões e sobre o perfil-base. (2026-07-15)
- **D19** — Equipes pertencem a uma única organização e possuem estado ativo, líder opcional,
  capacidade padrão e capacidade individual por membro. Desativar preserva histórico e vínculos;
  não realiza exclusão física. (2026-07-15)
- **D20** — Projetos possuem ciclo de vida explícito (planejamento, ativo, pausado, concluído
  ou cancelado), metodologia (Kanban, Scrum, Scrumban ou lista simples), datas e arquivamento
  lógico. Configurações, etiquetas e definições de campos personalizados pertencem ao projeto
  e toda mudança administrativa relevante gera histórico. (2026-07-15)
- **D21** — `WorkItem` permanece o agregado único para demandas internas e externas. Cada item
  recebe número estável gerado por sequence do SQL Server e pode registrar origem, solicitante,
  responsável principal, participantes, critérios de aceite, seguidores, links tipados
  (dependência/bloqueio/relação) e valores de campos personalizados. ~~Exclusão funcional é
  arquivamento lógico para preservar auditoria e apontamentos.~~ Após D53, excluir e arquivar são ações
  distintas. (2026-07-15)

- **D22** — Status de workflow pertence ao projeto e é separado da coluna visual. Cada `Stage`
  referencia um `WorkflowStatus` e cada `WorkItem` persiste o status atual; mover um cartão sincroniza
  ambos. Transições são direcionais e obrigatórias, e o limite de WIP da coluna é validado no backend.
  A migration cria status equivalentes às colunas existentes e libera as transições já possíveis para
  manter compatibilidade. (2026-07-16)
- **D23** — “Meu trabalho” será uma projeção de leitura calculada a partir do mesmo `WorkItem`, sem
  duplicar tarefas em uma fila própria. A projeção combina atribuição, autoria, acompanhamento,
  prazos, bloqueios, solicitações externas, aprovações e comentários; menções usam o token `@e-mail`.
  Alertas importantes são derivados desses dados até o módulo persistente de notificações da Fase 7.
  (2026-07-16)
- **D24** — Atualizações em tempo real do quadro usam SignalR com grupos por organização/quadro.
  O evento transporta apenas o identificador do quadro; clientes autorizados invalidam o cache e
  recarregam a projeção, mantendo regras e dados sensíveis na API. Preferências de conteúdo dos
  cartões são persistidas no próprio quadro como JSON versionável. (2026-07-16)
- **D25** — O ciclo de sprint passa a ser Planejada → Ativa → Concluída, com cancelamento a partir
  de Planejada ou Ativa, e somente uma sprint ativa por projeto. Ao concluir ou cancelar, o escopo
  é fotografado em `SprintItemSnapshot`; itens pendentes podem voltar ao Product Backlog ou seguir
  para outra sprint sem apagar métricas e resultados históricos. (2026-07-20)
- **D26** — O Portal Externo é configurado por projeto e quadro de entrada, mas cada solicitação
  continua sendo o mesmo `WorkItem` conforme D16/D21. Metadados públicos ficam em
  `ExternalRequest`: protocolo, chave de acompanhamento armazenada apenas como hash, conversa,
  avaliação e confirmação. O acesso pode usar link público, JWT, convite ou código de e-mail;
  códigos só retornam na resposta quando o host está em Development e não há SMTP configurado.
  (2026-07-20)
- **D27** — Formulários externos pertencem ao Portal Externo e persistem definição, campos,
  condições simples, limites de anexo e regras ordenadas de atribuição. Cada envio cria, numa única
  operação, o `ExternalRequest` e o mesmo `WorkItem` de origem externa, preservando os valores
  submetidos e anexos no item interno. A proteção de entrada combina rate limiting nativo do
  ASP.NET Core por endereço/IP, honeypot, tempo mínimo de preenchimento e listas permitidas de
  extensão/MIME. A triagem usa estado explícito e eventos append-only; toda ação administrativa
  fica auditada, inclusive recusa justificada, pedido de informação, roteamento e vínculos.
  (2026-07-20)
- **D28** — Comunicação externa e colaboração interna usam fronteiras persistentes distintas.
  `Comment` é sempre interno e só circula em endpoints autenticados da tarefa;
  `ExternalRequestMessage` é sempre uma resposta pública e pode ser devolvida ao solicitante.
  A consulta por protocolo usa um DTO público mínimo, sem triagem, roteamento, responsáveis,
  campos privados, eventos internos ou horas. Anexos só atravessam essa fronteira quando
  `IsExternalVisible` estiver explicitamente marcado. (2026-07-20)
- **D29** — O SLA pertence ao projeto e é aplicado às solicitações externas por uma política com
  calendário de atendimento, feriados e regras ordenadas por categoria/prioridade. Ao abrir a
  solicitação, os parâmetros e vencimentos são fotografados no `ExternalRequest`; alterações
  posteriores da política não reescrevem o histórico. Pedido de informação pode pausar o relógio,
  resposta do solicitante o retoma e apenas minutos úteis deslocam os vencimentos. (2026-07-20)
- **D30** — Apontamentos e relatórios de produtividade tratam somente duração, horas previstas,
  horas realizadas, capacidade e seus desvios. Valor-hora, salário, custo, faturamento e
  rentabilidade não fazem parte do modelo, dos DTOs nem das telas. Relatórios são projeções do
  mesmo `TimeEntry` e `WorkItem`, agrupadas por usuário, equipe e projeto. (2026-07-20)
- **D31** — O construtor de relatórios é declarativo e executado no servidor sobre um catálogo
  fechado de fontes, colunas, métricas, filtros, agrupamentos, ordenações e visualizações.
  `SavedReport` persiste a definição JSON validada, proprietário, projeto opcional e
  compartilhamento na organização; nunca armazena nem executa SQL fornecido pelo usuário.
  Dashboards são projeções dos mesmos agregados e aceitam filtros básicos, evitando tabelas de
  resumo divergentes. A exportação inicial usa CSV da projeção autorizada. (2026-07-20)
- **D32** — Notificações internas são registros persistentes por organização e destinatário,
  separados dos alertas derivados de “Meu Trabalho”. Preferências por tipo controlam os canais
  in-app e e-mail; o registro também funciona como fila durável de entrega, com chave de
  deduplicação para lembretes de prazo/SLA e processamento assíncrono sem bloquear o caso de uso.
  Mensagens transacionais do Portal Externo continuam seguindo a fronteira definida em D28.
  (2026-07-20)
- **D33** — A pesquisa global é uma projeção paginada e limitada no servidor sobre tarefas,
  solicitações, projetos, usuários, equipes e sprints, sempre sob tenant e permissões do usuário.
  Ela retorna somente metadados mínimos e destinos de navegação; comandos rápidos são um catálogo
  fechado do frontend e não aceitam código ou rotas fornecidas pelo usuário. (2026-07-20)
- **D34** — A auditoria transversal é imutável e gravada na mesma transação das alterações pelo
  `AppDbContext`, usando o contexto autenticado da requisição para usuário, origem, IP e correlação.
  Somente entidades funcionais previstas são auditadas; senhas, hashes, tokens, chaves, segredos e
  conteúdo binário são excluídos dos snapshots anterior/novo. Consultas de auditoria exigem
  permissão administrativa e permanecem isoladas por organização. (2026-07-20)
- **D35** — Refresh tokens são opacos, armazenados somente como hash, rotacionados a cada uso e
  enviados à SPA em cookie HttpOnly/SameSite; revogação de uma família invalida sua reutilização.
  Confirmação de e-mail e recuperação de senha usam tokens nativos e temporários do Identity,
  lockout contabiliza falhas reais e segredos de banco/JWT saem dos arquivos versionados para
  configuração externa. Erros HTTP seguem Problem Details, com mapa de campos em validações.
  (2026-07-20)
- **D36** — A hierarquia funcional apresentada ao usuário será **Organização → Equipe → Projeto →
  Tarefa → Subtarefa → Checklist**; em projetos de desenvolvimento, os tipos do mesmo `WorkItem`
  formam **Épico → História de usuário → Tarefa → Subtarefa**. `Board`, `Stage`, backlog e sprint
  ~~continuam como configurações e projeções operacionais do projeto, preservando D10/D21 e o banco
  existente~~ não criam níveis organizacionais adicionais; após D52, `Board`/`Stage` são operacionais e
  transversais, enquanto backlog e sprint permanecem ligados ao projeto. `Feature` permanece tipo opcional e
  nunca é um nível obrigatório. (2026-07-20)
- **D37** — O fechamento do MVP inclui ações em massa, automações básicas declarativas, autorização
  integral, acessibilidade e homologação. Automações aceitam somente gatilho de entrada em coluna e
  ações fechadas de atribuir, mover, priorizar ou etiquetar, com validação de alvos, workflow/WIP,
  detecção de ciclos, limite defensivo e auditoria. Permanecem fora do MVP IA, finanças, chat/wiki,
  mobile nativo, CI/CD/Git próprios, Gantt/roadmap avançados, BI/SQL livre, fórmulas complexas,
  monitoramento invasivo, microsserviços e automações avançadas ou com scripts. (2026-07-20)
- **D38** — As dependências do fechamento do MVP permanecem nas famílias estáveis compatíveis com
  .NET 8, mas recebem as correções de manutenção e segurança mais recentes dessas famílias. Quando
  uma dependência transitiva vulnerável não puder ser eliminada pelo pacote de nível superior, ela
  será fixada explicitamente na menor família estável compatível e coberta por build, testes e
  auditoria de pacotes. Não serão usadas versões preview. (2026-07-20)
- **D39** — Testes E2E com **Playwright** (@playwright/test). Chromium em cada PR; Firefox/WebKit
  em execucao noturna ou pre-release. Autenticacao via setup project que faz login pela API e
  persiste storageState. baseURL configuravel por variavel de ambiente. Trace on-first-retry,
  screenshot e video em falha, relatorio HTML. Sem sleeps fixos; locators por role, label ou
  data-testid estavel. Nunca executar contra producao. (2026-08-19)
- **D40** — Toda tarefa que modifique frontend (React), endpoints da API (.NET) ou schema de banco de dados
  deve ter os testes E2E executados e passando como parte da Definition of Done. Antes de considerar a
  tarefa concluída, execute `npm run e2e` em `src/Prisma.Workspace.Web` com o ambiente E2E ativo (API +
  frontend + SQL Server). Se os testes falharem, a tarefa não está completa até que as falhas sejam
  corrigidas ou novos testes sejam adicionados para cobrir a funcionalidade implementada. Esta regra
  não se aplica a tarefas que modificam apenas documentação, configurações de infraestrutura sem impacto
  no comportamento, ou refatorações internas sem mudança de interface. (2026-08-19)
- **D41** — O Agent Loop pode executar automaticamente somente os product gates ativos e versionados em
  `profiles/runrun-loop.yaml`. A execução ocorre apenas no estado `gating`, respeita a ordem declarada,
  interrompe na primeira falha quando configurado, registra resultado/duração/código de saída no envelope
  e na telemetria e nunca persiste stdout/stderr. Não existe entrada para executar comandos arbitrários,
  autoaprovar Human Gates, chamar agentes/LLMs, aplicar migrations, criar PR, fazer merge ou deploy.
  Caminhos de trabalho devem permanecer dentro do repositório. (2026-08-20)
- **D42** — XP não será adicionado nem exibido por enquanto. As estruturas de trabalho continuam sendo
  somente Kanban, Scrum, Scrumban e Lista simples, com os valores definidos na D20. (2026-08-20)
- **D43** — Em projetos Kanban, Story Points ficam ocultos e não editáveis em todas as superfícies do
  produto, incluindo detalhe, cards, backlog, sprint, dashboards e relatórios. O campo permanece no
  backend apenas para retrocompatibilidade e preservação de dados. (2026-08-20)
- **D44** — O detalhe da tarefa será uma única gaveta com seis abas: Descrição, Comentários, Subtarefas,
  Anexos, Histórico e Grafo de Estados. Comentários são conversas humanas; Histórico contém auditoria
  automática; o Grafo de Estados é uma projeção interativa do histórico feita com React Flow.
  Participantes autorizados podem editar, mover e apontar horas, mantendo o responsável principal como
  dono accountable da tarefa. (2026-08-20)
- **D45** — A padronização de workflow será multitenant e dinâmica no sentido **Organização → Projeto**.
  A organização mantém templates de status e transições; cada projeto opera em modo `Inherited` ou
  `Custom`. Projetos herdados acompanham alterações compatíveis do template vinculado, enquanto a primeira
  personalização explícita cria uma configuração independente. Projetos legados entram como `Custom` para
  preservar integralmente o comportamento existente. (2026-08-20)
- **D46** — O nome funcional exibido para responsáveis e participantes pertence à associação do usuário com
  a organização (`OrganizationMember.DisplayName`), permitindo identificação adequada por tenant. O campo é
  editável por administrador da organização. O fallback aceita `UserName` somente quando não tiver formato
  de e-mail; caso contrário, usa o rótulo neutro `Usuário {shortId}`. O e-mail fica restrito a contexto
  administrativo secundário e nunca é o nome principal em superfícies de tarefa. (2026-08-20)
- **D47** — A auditoria de tarefas preserva autoria por snapshot: eventos e movimentações registram o
  `ActorId` quando disponível e o `ActorName` observado no momento da ação, sem FK obrigatória que permita
  apagar autoria ou impedir o ciclo de vida do usuário. Comentários continuam separados do histórico
  automático. (2026-08-20)

- **D48** — A escolha de metodologia/estrutura de trabalho fica suspensa na experiência do produto. A interface
  não deve exibir, permitir selecionar ou editar Kanban, Scrum, Scrumban ou Lista simples. Projetos novos usam
  internamente `ProjectMethodology.Kanban` como valor compatível; projetos existentes preservam o valor já
  persistido. O enum, a coluna e os contratos permanecem temporariamente para retrocompatibilidade, sem migration.
  Esta decisão substitui somente a parte visível de metodologia prevista em D20/D42 e em
  `SPEC-PROJECT-STRUCTURE`. (2026-08-20)

- **D49** — O detalhe da tarefa substitui a gaveta lateral definida na D44 por um modal centralizado, amplo,
  responsivo e organizado nas mesmas seis abas: Descrição, Comentários, Subtarefas, Anexos, Histórico e Grafo
  de Estados. A D44 permanece válida quanto ao conteúdo, separação e permissões; somente o contêiner visual é
  sucedido. O modal preserva deep link, histórico do navegador, foco, teclado e contexto de origem. (2026-08-20)

- **D50** — ~~Quadro é uma projeção operacional do projeto, não o proprietário da tarefa. Um `WorkItem` é um
  agregado canônico do projeto, deve possuir ao menos uma associação de quadro e pode aparecer em vários
  quadros sem duplicação. Cada projeto possui um quadro padrão e novas tarefas entram em seu `Backlog` quando
  não houver seleção explícita. A posição é específica da projeção; o `WorkflowStatusId` permanece canônico e
  uma transição só é válida se todos os quadros associados puderem representá-la.~~ Sucedida pela D52. O modelo
  N:N permanece somente como estado implantado a migrar. (2026-08-20)

- **D51** — O shell da aplicação deixa de usar barra lateral persistente. A navegação global migra para uma
  barra superior e os controles específicos da tela, incluindo breadcrumbs, filtros, ordenação e troca de
  visão, ocupam uma segunda faixa contextual. No mobile, a navegação usa superfície sobreposta acionada pelo
  topo, sem reservar uma coluna lateral. Rotas, permissões e capacidades existentes são preservadas. (2026-08-20)

- **D52** — Quadro passa a seguir o conceito operacional transversal aprovado a partir da referência oficial do
  Runrun.it: pertence à organização, pode reunir tarefas de projetos diferentes e não é filho funcional de um
  projeto. Cada tarefa mantém uma única posição atual em um quadro/coluna; transferi-la para outro quadro
  preserva integralmente conteúdo e histórico e permite escolher a coluna de destino ou usar a primeira coluna
  aberta. Colunas têm nome e ordem livres, sem nomes/templates obrigatórios, e são classificadas internamente
  como abertas ou concluídas; entrar em concluída conclui a tarefa e voltar para aberta a reabre. O Kanban é a
  tela inicial autenticada, abrindo o último quadro acessado; no primeiro acesso ou se ele não estiver disponível,
  exibe `Nenhum quadro selecionado` com `Selecionar quadro` e, mediante permissão, `Criar quadro`. O seletor
  superior lista os quadros acessíveis. Somente usuários/equipes vinculados visualizam um quadro e veem todas as
  suas tarefas; esse acesso concede aos projetos representados o mesmo nível de permissão do quadro. Criar,
  editar, arquivar quadros e administrar colunas usa a permissão configurável **Administrar quadros**, não papel
  fixo. Arquivar um quadro arquiva também suas tarefas e as oculta das telas normais; somente administradores
  restauram o conjunto. Filtros usam painel lateral contextual recolhível/overlay (sem sidebar global), lembram
  aberto/fechado por usuário e só afetam resultados após `Aplicar`. Após D64, filtros salvos são exclusivamente
  pessoais, com escopo no quadro atual ou em qualquer quadro acessível. A D52 sucede D10/D50 e a cláusula de
  quadros da D36 no que conflita, restringe D22/D45 ao
  modelo legado/compatibilidade quando
  relacionam coluna a workflow de projeto e preserva a D51 quanto à ausência de sidebar global persistente.
  (2026-08-24)

- **D53** — Na gestão de tarefas, criação exige título, projeto, responsável principal, quadro e coluna; a tarefa nunca
  fica sem responsável e removê-lo exige substituto. Participantes autorizados podem editar, mover, concluir e
  apontar horas; excluir, arquivar e restaurar ficam restritos ao responsável principal e administradores. Pai
  só conclui quando todas as subtarefas estiverem concluídas, com ação atômica para concluir todas e então o pai,
  mas seu status é independente nos demais casos. `Excluir` envia à lixeira por 7 dias e é diferente de
  `Arquivar`; ao excluir pai, o usuário escolhe incluir subtarefas ou preservá-las, reatribuindo-as à avó quando
  existir e tornando-as independentes caso contrário. Restaurar família excluída junta restaura toda a família.
  Arquivadas aparecem em filtro e podem ser restauradas. Edição concorrente usa `last write wins`. Esta decisão
  sucede a equivalência entre exclusão e arquivamento da D21. (2026-08-24)

- **D54** — Product Backlog lista por padrão tarefas sem sprint e oferece filtro para incluir tarefas já
  planejadas em sprint. Kanban e sprint são recortes do mesmo `WorkItem`; a tarefa pode permanecer no Kanban
  sem `SprintId`. Excluir uma sprint remove apenas o vínculo `SprintId`, preserva quadro, coluna, conteúdo e
  histórico da tarefa e faz os itens voltarem ao Product Backlog padrão. (2026-08-24)

- **D55** — A autorização funcional passa a ter cinco perfis-base por organização: **Administrador, Gestor,
  Membro, Visualizador e Externo**. Administrador possui autoridade funcional total no tenant; Gestor administra
  a operação, projetos, quadros/colunas, equipes, sprints, relatórios, custos e aprovadores nos escopos
  autorizados, mas não controla autoridade máxima nem segredos; Membro executa o trabalho autorizado;
  Visualizador somente lê; Externo usa exclusivamente portal/protocolo/token. Administradores e gestores podem
  criar perfis personalizados reutilizáveis por conjunto de permissões, sem conceder autoridade que não possuem.
  `ScrumMaster`, `ProductOwner`, `Developer` e `ProjectManager` deixam de conceder acesso como perfis-base e
  podem existir apenas como função/cargo. Não haverá hierarquia fixa duplicada por projeto: permissões usam os
  escopos organização, equipe, projeto, **quadro**, tarefa, relatório, formulário e solicitação. Acesso ao quadro
  deriva para os projetos representados no mesmo nível conforme D52. A resolução valida tenant e limites
  estruturais, aplica qualquer deny explícito antes de allow explícito, perfis personalizados e perfil-base, e
  usa default deny. Esta decisão sucede D12 e especializa D18, preservando seu modelo híbrido, menor privilégio e
  prevalência de negação explícita. (2026-08-24)

- **D56** — A chave de projeto deixa de ser informação funcional da experiência. Não será exibida no formulário
  de criação, cartões, cabeçalhos, breadcrumbs, seletores, filtros, relatórios ou demais telas operacionais, nem
  poderá ser informada ou editada pelo usuário. O backend permanece como única autoridade para gerar uma chave
  técnica estável e única por organização. `Project.Key`, seu índice e contratos internos podem ser preservados
  enquanto forem necessários para banco, unicidade, importação, busca técnica, compatibilidade ou integrações;
  removê-los futuramente exige auditoria de consumidores e, se houver alteração de schema, `G-MIGRATION`.
  (2026-08-24)

- **D57** — Criar, editar, arquivar e restaurar projetos é capacidade-base de **Administrador e Gestor** conforme
  D55; Membro, Visualizador e Externo não a possuem por padrão. Um projeto pode vincular várias equipes e vários
  membros individuais. A criação exige somente nome e descrição; a chave técnica é automática e oculta conforme
  D56. Vincular uma equipe concede automaticamente perfil-base Membro no projeto a todos os seus integrantes;
  ao sair da equipe, a pessoa perde esse acesso herdado, exceto quando ainda possuir vínculo individual ou acesso
  por outra equipe. Projeto possui somente os estados `Ativo` e `Arquivado`, pode nascer sem quadro e não cria
  quadro automático, exclusivo ou padrão; o quadro transversal independente é escolhido depois conforme D52.
  A relação Projeto↔Quadro é exclusivamente derivada pelas tarefas presentes: criar ou mover uma tarefa para um
  quadro exige acesso ao projeto e ao quadro, faz o projeto aparecer nos filtros desse quadro e, quando sua última
  tarefa sair, a relação visual desaparece. Não existe vínculo manual persistente e o quadro não é dono do projeto.
  Arquivar projeto é operação lógica, transacional e recuperável: oculta o projeto e todas as suas tarefas das
  telas normais, sem apagar dados, horas, anexos, sprints ou histórico e sem arquivar os quadros independentes nos
  quais elas estavam. A restauração por Administrador ou Gestor reativa somente recursos arquivados pela mesma
  operação e preserva os que já estavam arquivados. Projeto não possui lixeira nem exclusão permanente; seu ciclo
  de retirada e retorno é exclusivamente arquivar/restaurar. (2026-08-24)

- **D58** — `Organization` é o tenant raiz equivalente à conta/empresa: dentro dela ficam usuários, equipes,
  projetos, quadros, tarefas, configurações e relatórios, sempre sob isolamento estrito. Usuário comum pertence
  a exatamente uma organização: o mesmo login/e-mail não pode possuir memberships em tenants diferentes e não
  vê seletor de organizações. **Administrador da plataforma** é uma autoridade de control plane externa aos
  perfis da D55, pode alternar tenants pelo seletor superior e é o único que pode criar, editar, arquivar e
  restaurar organizações; essa autoridade não pode ser concedida por perfil ou permissão do tenant. Na troca
  administrativa, contexto, cache e respostas pendentes do tenant anterior são descartados. Organização
  arquivada preserva todos os dados, fica indisponível aos membros e opera somente em leitura para Administradores
  da plataforma até ser restaurada. Não existe exclusão física de organização. A D58 especializa D17/D55,
  substitui a hipótese anterior de múltiplas memberships comuns e incorpora a antiga
  `SPEC-ORGANIZATION-SWITCH-REFRESH` à `SPEC-ORGANIZATIONS`. (2026-08-24)

- **D59** — Dependências, pré-requisitos e bloqueios entre tarefas ficam ocultos e sem função operacional nesta
  fase. A interface não exibe nem permite criar, editar, remover ou pesquisar dependências; vínculos preservados
  não bloqueiam tarefas e não alimentam workflow, automações, notificações, filtros, indicadores, dashboards ou
  relatórios. Estruturas, código defensivo e dados internos atuais podem permanecer temporariamente apenas por
  compatibilidade/auditoria, sem migration ou limpeza agora e sem inferir novos vínculos em importações. Reativar
  a funcionalidade exige nova decisão e `G-SCOPE`; remover ou transformar schema/dados exige `G-MIGRATION`.
  (2026-08-24)

- **D60** — A ordem das colunas permanece livre e compartilhada conforme D52. Dentro de cada coluna, cartões
  possuem uma ordem-base manual, persistida e comum a todos os usuários autorizados; uma tarefa recém-criada
  entra no topo e pode ser reordenada verticalmente depois. Data de criação ou outra ordenação automática não
  substitui essa base. Filtros produzem somente uma visão pessoal e temporária: reordenar verticalmente o
  subconjunto filtrado não grava a posição compartilhada, não afeta outros usuários, não integra filtro salvo e
  é descartado ao alterar ou remover o filtro. Com filtro ativo, mover a tarefa entre colunas continua sendo uma
  alteração real e persistente; apenas sua ordenação vertical na visão filtrada é temporária. Esta decisão
  substitui a ordem institucional fixa e o padrão “Mais recentes” antes documentados em
  `SPEC-KANBAN-VISUAL-ORDER`, sem alterar workflow, WIP ou o caráter transversal do quadro. (2026-08-24)

- **D61** — O detalhe da tarefa mantém seis abas: **Descrição, Comentários, Subtarefas, Anexos, Histórico e Linha
  do tempo**. A Linha do tempo substitui o `Grafo de estados` por uma lista simples, textual e legível, limitada
  à criação na coluna inicial e às movimentações entre colunas. A ordem é da movimentação mais recente para a
  mais antiga; cada movimento mostra data/hora, pessoa, coluna anterior e nova coluna, e o evento inicial aparece
  como `Tarefa criada na coluna X`. Comentários permanecem somente em sua aba e fora do histórico automático
  conforme D47; Histórico continua sendo a auditoria geral separada. Esta decisão sucede somente a projeção em
  React Flow da D44/D49 e preserva o modal centralizado, a auditoria e os dados históricos existentes. (2026-08-24)

- **D62** — Reclassificar uma coluna de aberta para concluída é uma operação de impacto coletivo. Se houver
  tarefas na coluna, a interface exige confirmação com contagens do impacto e, ao confirmar, conclui todas as
  tarefas aplicáveis na mesma transação da reclassificação. Quando uma tarefa-pai afetada possuir descendentes
  abertos em qualquer nível, inclusive em outras colunas, a confirmação deve perguntar explicitamente se toda a
  descendência recursiva também será concluída; sem esse consentimento, a reclassificação inteira é cancelada.
  Reclassificar a coluna de concluída para aberta também exige confirmação e reabre todas as tarefas nela. Nos
  dois sentidos, classificação e estados das tarefas mudam em uma única transação, cada tarefa alterada recebe
  seu próprio histórico e o impacto/concorrência é revalidado antes de persistir. A operação coletiva é coerente
  com o movimento individual da D52/D53: coluna concluída implica tarefa concluída; coluna aberta implica tarefa
  aberta. (2026-08-24)

- **D63** — Nome completo é obrigatório no cadastro de novos usuários. Toda interface e todo histórico exibem o
  nome como identificação principal e ocultam o e-mail quando o nome existir. E-mail pode aparecer como fallback
  somente para dados legados sem nome; ausência de ambos usa identificação neutra sem inventar autoria. Snapshots
  históricos preservam o nome observado no momento do evento. Esta decisão especializa a regra de identificação
  funcional da D46/D47; contratos de autenticação e cadastro serão detalhados em revisão própria. (2026-08-24)

- **D64** — Filtros salvos são exclusivamente pessoais: somente o proprietário pode visualizar, criar, aplicar,
  editar, renomear, duplicar ou excluir, sem opção de compartilhar com quadro, equipe, projeto, organização ou
  outra pessoa. Ao salvar, o usuário escolhe escopo `Quadro atual` ou `Qualquer quadro acessível`. No escopo
  global pessoal, cada critério é revalidado no quadro corrente; critérios específicos incompatíveis são
  ignorados somente naquele quadro, sem impedir os demais, permanecem na definição e voltam a valer em quadros
  compatíveis. A interface mostra aviso e chip identificando cada critério ignorado. Permanecem válidos o botão
  `Aplicar`, a preferência pessoal aberto/fechado do painel e a D60: ordem vertical filtrada é temporária e não
  integra o filtro salvo, enquanto mover tarefa entre colunas continua real. Esta decisão sucede a opção de
  filtro compartilhado antes admitida na D52 e em `SPEC-SEARCH-SAVED-FILTERS`. (2026-08-24)

- **D65** — A tarefa não possui status funcional separado do quadro. Seu status textual canônico é exatamente o
  nome da coluna atual; a classificação interna da coluna determina apenas se a tarefa está aberta ou concluída
  e não aparece como etiqueta principal genérica. Escolher status no detalhe/dropdown significa mover a tarefa
  para a coluna escolhida, e arrastar no Kanban atualiza a mesma informação em todas as demais telas. Não pode
  haver divergência entre coluna e status exibido. Reclassificar uma coluna atualiza aberta/concluída de todas as
  tarefas conforme a operação bidirecional e atômica da D62, sem criar status adicional. `WorkflowStatus`,
  templates, transições e campos relacionados permanecem somente como legado técnico até migração segura e não
  governam nomes, exibição ou movimento. A D65 sucede D22/D45 e o alvo anterior da
  `SPEC-WORKFLOW-STATUS` no que definia status independente por projeto. (2026-08-24)

- **D66** — ~~A proposta de renovação visual da lista de projetos foi rejeitada. O visual atual do produto deve ser
  preservado, e a `SPEC-PROJECTS-VISUAL-REFRESH` fica encerrada como `superseded`, sem aprovação, implementação
  ou `G-SPEC` pendente. A `TASK-033` fica cancelada. Correções funcionais, de acessibilidade ou responsividade
  permanecem possíveis somente quando cobertas por outra spec aprovada e não reativam essa proposta estética.~~
  Parcialmente sucedida pela D68/D69: a renovação estética genérica permanece rejeitada, mas compactação e busca
  na lista de projetos passam a ser permitidas no rebrand Prisma. (2026-08-24)

- **D67** — O desenvolvimento AI-Native passa a usar o fluxo **História de usuário → Especificação gerada pela IA →
  G-SPEC → Tarefas → Contexto → Implementação → Testes → Homologação da história**. A história registra primeiro a
  necessidade e o comportamento esperado; a spec transforma esse material em contrato funcional e técnico, continuando
  a ser a única unidade aprovada formalmente pelo `G-SPEC`. Histórias não recebem gate nem aprovação individual: na
  homologação ficam como `Ainda não testada`, `Está conforme`, `Precisa de ajuste`, `Não implementada` ou `Bloqueada`.
  Toda observação salva, marcação `Precisa de ajuste` ou `Não implementada` cria ou atualiza uma tarefa rastreável para
  análise da IA, vinculada à história e à spec. A IA classifica se há defeito, ausência de implementação, mudança da
  própria história/spec ou impedimento de teste, sem duplicar tarefa ativa. Mudança de requisito volta para a história e
  exige nova versão da spec e novo `G-SPEC` antes de código; defeito ou ausência cobertos pela spec voltam para a fila de
  implementação. Após a correção, a tarefa é concluída e arquivada, a observação sai da área ativa preservando histórico
  e a história volta para nova homologação. (2026-08-26)

- **D68** — O produto deixa de ser ferramenta interna do Detran-SE e passa a ser **Prisma WorkSpace**, plataforma open source voltada a Community / Cloud / Enterprise, com monetização por consultoria, treinamentos, suporte e customizações (Nordevs / time do produto). A identidade visual oficial passa a seguir a LP Prisma e o mockup de login: espectro violeta→azul→cyan→magenta→laranja, gradiente de marca, tipografia Inter, marca prismática, light-first com modo escuro opcional, e textos sem menção institucional ao Detran. A compatibilidade temporária dos namespaces e pastas C# legados foi encerrada pela migração técnica D76; a superfície e a identidade técnica do produto usam Prisma. A D68 sucede D9 e a cláusula de identidade da D13. (2026-09-02; atualizada em 2026-09-05)

- **D69** — Feedback funcional do documento de análise (login/marca, remoção de anexos, responsável no topo da tarefa, listas mais compactas/pesquisáveis) entra no backlog de produto Prisma sem reabrir a renovação estética rejeitada em D66. A exclusão de anexos de tarefa fica disponível na UI com confirmação; a retenção completa em lixeira por 7 dias (SPEC-ATTACHMENTS) permanece gap até `G-MIGRATION`. Cards de projetos podem ficar mais compactos e com busca, sem exibir chave de projeto (D56). Demais itens do documento (Backlog/Sprint lado a lado, Sprints, Relatórios em abas, Configurações com menu lateral, etc.) seguem como tarefas posteriores cobertas por specs. (2026-09-02)

- **D70** — O PO reabre explicitamente a evolução visual sistêmica do Prisma WorkSpace, incluindo login, shell e
  principais superfícies autenticadas. A mudança deve aumentar hierarquia, densidade útil, consistência e qualidade
  percebida com base em pesquisa de aplicações de gestão, sem copiar outro produto nem adicionar funcionalidades não
  aprovadas. A identidade D68 e a topbar sem sidebar global permanecem invariantes. D70 sucede a proibição estética
  residual da D66 e o limite visual da D69; implementação continua condicionada à aprovação da
  `SPEC-PRISMA-VISUAL-SYSTEM` no `G-SPEC`. (2026-09-03)

- **D71** — A primeira entrega de consultas por projeto usa um compositor visual, definição JSON interna, filtros
  pessoais salvos e estado não sensível serializado na URL. Não inclui linguagem textual, SQL livre, API pública ou
  exportação. Sem criar schema, `SavedFilter` é reutilizado no quadro padrão — ou primeiro quadro — do projeto como
  âncora técnica, com `scope: project-items-v1`; filtros desse escopo não aparecem como filtros comuns do Kanban.
  Projetos sem quadro podem executar a consulta e compartilhar sua URL, mas não salvá-la. (2026-09-03)

- **D72** — A entrada autenticada de pessoas com acesso ao workspace passa a ser `/home`, uma visão geral acionável
  baseada exclusivamente em dados reais de `Meu trabalho` e `Projetos`. O Kanban permanece destino operacional e pode
  ser retomado em um clique, mas deixa de ser a primeira tela obrigatória. O papel de portal externo (`role 8`) continua
  entrando em `Solicitações`. Esta decisão sucede somente a cláusula “Kanban é a tela inicial autenticada” da D52 e
  preserva todas as demais regras de quadro da D52. (2026-09-03)

- **D73** — A organização do trabalho passa a distinguir três naturezas funcionais escolhidas explicitamente na
  criação: **Projeto**, para iniciativa temporária com início, fim e entrega; **Melhoria**, para evolução incremental
  ou contínua; e **Sustentação**, para operação, suporte, manutenção ou correção. As subdivisões internas continuam
  configuráveis pela pessoa autorizada e não são geradas nem sugeridas por IA. A implementação depende da definição
  técnica na `SPEC-WORK-NATURE`, de `G-SPEC` e de `G-MIGRATION` com estratégia de compatibilidade para dados existentes;
  eventual automação de etapas também exige `G-WORKFLOW`. (2026-09-03)

  **Complemento aprovado:** a estrutura classificada é exatamente o `Project`; `Solicitações` permanece como módulo
  externo e não será substituído por uma entidade central `Demand`. Cada projeto também recebe um Tipo de Trabalho
  fechado entre Desenvolvimento, Infraestrutura, Banco de Dados, Suporte, Segurança, Dados/BI, Integração,
  Documentação e Gestão. Novos projetos exigem os dois valores; registros anteriores ficam tecnicamente como
  `Não classificado` até edição explícita para evitar atribuição falsa. As classificações são exibidas e filtráveis,
  podem ser alteradas pela gestão autorizada com auditoria e não alteram workflow automaticamente. (2026-09-04)

- **D74** — O endereço público canônico e exclusivo do Prisma WorkSpace passa a ser
  `https://prisma.nordevs.com.br`. O host `runrun.nordevs.com.br` foi removido do Caddy após validação do novo DNS e
  não funciona como alias; seu registro DNS legado deve ser removido da zona. Links gerados pela aplicação usam o
  domínio Prisma. O TLS é terminado no Caddy do VPS e encaminha para `detran-kanban-api:8080`. (2026-09-04)

- **D75** — A distribuição open source do Prisma WorkSpace nasce em um novo repositório `prisma-workspace`, criado
  inicialmente como privado a partir de snapshot sanitizado por allowlist. O histórico do repositório `runrun` não
  será importado, reescrito nem publicado e permanecerá como arquivo privado recuperável. A promoção pública exige
  auditoria de segredos/dados institucionais, escolha humana da licença e `G-DEPLOY`. Cursor CLI pode executar lotes
  mecânicos delimitados, mas não aprova gates, decide arquitetura/licença, acessa segredos ou publica sem revisão.
  Migrations aplicadas são preservadas até estratégia específica aprovada em `G-MIGRATION`. (2026-09-05)

- **D76** — No novo repositório, solution, projetos, pastas, assemblies e namespaces técnicos adotam o prefixo
  `Prisma.Workspace`. A mudança é exclusivamente de identidade técnica: IDs e classes históricas de migrations,
  schema, tabelas, nomes de banco existentes e comportamento não são alterados. Configurações e nomes de banco
  ainda legados serão tratados separadamente na instalação independente, com `G-MIGRATION` quando houver impacto
  persistente. (2026-09-05)
