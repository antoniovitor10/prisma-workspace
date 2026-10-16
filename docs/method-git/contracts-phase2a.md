# Contratos F0/F1 — método Git 2a

Fonte: US-METODO-GIT-001, SPEC-METODO-GIT-INTEGRATION, GOALS/D102 e revisão git-spec-2. Forja detém este arquivo, enums, DTOs e interfaces de F0. Ponte solicita extensões por ESTADO e não altera esses arquivos. O F1 integra os endpoints e a persistência sob `MethodGit:Enabled`, desligada por padrão; a liberação exige revisão, suítes completas e QA independente no SHA final.

## Acréscimos compatíveis do F1

- `GET /api/setup/status` inclui `methodEnabled` para apresentar a configuração do método somente nas instalações habilitadas. Não revela segredos nem provisiona projetos.
- `MethodIdentityDto.capabilities` contém os nomes das permissões, como `View`, `ChangeStatus` e `ManagePermissions`, inclusive para a sessão UI antes do provisionamento. Um JWT em rota pública de portal não estabelece autorização interativa de projeto; a origem pública continua desconhecida para concessão de execução.
- `MethodProjectSnapshot.identityIssues` é opcional. A fachada retorna diagnóstico de IDs inválidos/duplicados ao administrador humano da UI, incluindo colisões com cards arquivados e de outros quadros do projeto. Não escolhe candidato nem renumera cards. Os consumidores de Git/MCP podem ignorar esse campo.
- `MethodTaskDto.resumeApprovalEventId` é opcional, para apresentar uma liberação vigente depois de recarregar a UI. Repetir a liberação do mesmo bloqueio/ciclo retorna o evento existente. Nenhuma liberação move o card; nenhum token do método a concede.
- O provisioner de estrutura `IMethodProjectProvisioner`/`MethodProjectProvisioner` e seu teste SQL pertencem à Ponte, conforme transferência expressa em ESTADO em 16/10. Fachadas, controllers de identidade/settings/tasks, DI, guard, backfill, serviço de movimento e migration permanecem com a Forja.
- Criação/edição/comentário notificam o quadro após persistir. A operação dedicada de mover mantém a execução das automações existentes com o principal iniciador; cada passo passa pela barreira comum. Worker Git não recebe autorização humana por esse caminho.

## Identidade e segurança

- Credencial opaca: `pmt_` seguido de43 caracteres base64url (32 bytes aleatórios, 256 bits). Não é JWT. O servidor guarda SHA-256 dos47 caracteres completos e só exibe o segredo uma vez. `TokenPrefix` é apresentação parcial, não autenticação.
- `MethodRole` usa Human1, Agent2, System3 no servidor/banco. Nos DTOs de identidade e entrada Role: `humano`, `agente`, `sistema`. Não aceitar conversão de PRISMA_ROLE em autoridade.
- `authenticationKind`: `uiSession`, `methodToken`, `integrationWorker`. MCP exige `methodToken`, recusa JWT de login localmente pelo formato e confirma o tipo remoto, sem decodificar JWT. Token humano também não concede autorização/liberação interativa.
- Principal agente/sistema: `userId`/ActorId=`method:<PrincipalId em formato D>`, DisplayName próprio; nunca usar o emissor como autor. UI e token humano usam o próprio Identity UserId, com PrincipalId separado quando houver token. Escopo de um projeto. Member no máximo para agente, com teto/permissões atuais do delegante.
- Tokens: padrão30 dias, faixa1..90 (400 fora dela, validada no DTO e no serviço). Metadados LastUsed em janela5min; cada requisição verifica revogação/expiração/escopo, sem cache que atrase revogação. Rotação usando PrincipalId existente preserva autoria: humano exige HumanUserId igual ao chamador UiSession (outro humano403); agente/sistema exigem mesmo projeto/organização e principal ativo (outro projeto/inativo403). Papel/nome divergentes dão400. Reemissão cruza o teto anterior com permissões atuais do novo emissor e do delegante original, nunca a união nem ampliação. Reduzir o teto do principal também reduz seus tokens anteriores; uma nova permissão requer novo principal/delegação explícita, sem rotação que amplie. A autenticação/operação revalida os direitos atuais do emissor de cada token e do delegante, sem usar suas identidades como autor.
- Constantes MethodChannels evitam variação de grafia. Canal: sessão humana=`ui`, credencial de método=`mcp` (canal lógico da integração de método, inclusive REST direto), processamento de fato Git=`webhook`. Header/body não escolhe canal. Worker inicializa tenant/principal/correlação persistidos antes de dados filtrados.
- Erros: JSON ProblemDetails com status/code/mensagem pt-BR;401 inválido/expirado/revogado,403 autoridade/escopo/matriz,409 conflito/concorrência,400 validação,413 corpo,503 persistência/provedor indisponível. Nunca incluir token, segredo, PEM ou corpo bruto.
- Credencial do método não pode usar endpoints de autenticação/refresh para emitir uma sessão humana, nem se houver cookie humano junto; a autorização por token não é trocada silenciosamente por outra credencial. F1 cobre AuthController/middleware em teste HTTP próprio sem mudar o login/refresh de sessões humanas normais.
- Não autorizar por método de autenticação desconhecido. UI humana mantém suas permissões/validações atuais em todos os projetos. Produção desligada por padrão; QA apenas prisma-metodo com App simulada.

## Rotas e DTOs HTTP camelCase

| Método/rota | Credencial e DTO/retorno | Dono |
|---|---|---|
| GET `/api/method/me` | Método autenticado, descoberta de escopo sem JWT; MethodIdentityDto. Nome/ID configurado no MCP precisa corresponder ao projeto e quadro retornados. | Forja A/F1 |
| GET `/api/projects/{projectId}/method/me` | Identidade no projeto; não aceitar escopo diferente do token. MethodIdentityDto | Forja A/F1 |
| GET `/api/projects/{projectId}/method/rules` | MethodRulesDto (identity, project com mappings/campos, allowedTransitions, conventions e exigências interativas) | Forja A/F1 |
| GET `/api/projects/{projectId}/method/settings` | Estrutura existente ou estado desabilitado, sem provisionar por leitura | Forja A/F1 |
| POST `/api/projects/{projectId}/method/provision` | Somente UiSession admin; ProvisionMethodProjectRequest; cria/resolução explícita de quadro7 colunas/campos6, sem converter legado automaticamente; retorna MethodProjectSnapshot | Forja A/F1 |
| PUT `/api/projects/{projectId}/method/settings` | UiSession admin; UpdateMethodProjectRequest (enabled, autonomyLevel). Sem novo status na task | Forja A/F1 |
| GET/POST `/api/projects/{projectId}/method/tokens` | UiSession admin. GET retorna MethodTokenDto[]. POST IssueMethodTokenRequest; humano só para si; retorna IssuedMethodTokenDto `{metadata,token}` uma vez | Forja A |
| POST `/api/projects/{projectId}/method/tokens/{tokenId}/revoke` | UiSession admin,204. Revoga no próximo request; nenhuma credencial do método emite outra | Forja A |
| GET `/api/projects/{projectId}/method/tasks?status=...` | Array MethodTaskDto, somente topo/escopo, status opcional | Forja A/F1 |
| GET `/api/projects/{projectId}/method/tasks/next` | Array MethodTaskDto; em-desenvolvimento, autorização vigente e dependências concluídas | Forja A/F1 |
| GET `/api/projects/{projectId}/method/tasks/{id}` | ID T-XXXX, MethodTaskDto; campo HU e tag, sem HU-card | Forja A/F1 |
| POST `/api/projects/{projectId}/method/tasks` | CreateMethodTaskRequest, MethodTaskDto. Agente só backlog/concluida; chave única sincronizada, HU/tag, sem ParentId. Operação é explícita, não setup por leitura | Forja A/F1 |
| PUT `/api/projects/{projectId}/method/tasks/{id}` | UpdateMethodTaskRequest → MethodTaskDto; atualiza título/HU/arquivo/cobre/risco preservando ID/status/PR/restante. Substitui só a tag HU anterior e preserva tags extras. Escopo e permissão efetivos, sem conceder catálogo global ao agente | Forja A/F1 |
| POST `/api/projects/{projectId}/method/tasks/{id}/move` | MoveMethodTaskRequest, MethodTaskDto; matriz/autoridade real. Move genérico também recebe guard, não é bypass | Forja A/F1 |
| POST `/api/projects/{projectId}/method/tasks/{id}/comments` | MethodCommentRequest → MethodCommentDto | Forja A/F1 |
| POST `/api/projects/{projectId}/method/tasks/{workItemId:guid}/resume-approval` | UiSession com permissão atual de mover; ResumeMethodTaskRequest → ResumeMethodTaskApprovalDto. Liberação não move; tokens de qualquer papel403 | Forja A/F1 |
| POST `/api/projects/{projectId}/method/tasks/{id}/pull-requests` | LinkMethodPullRequestRequest; valida repo/URL e retorna TaskGitLinkDto; tabela/campoPR/comentário atômicos. Não buscar URL arbitrária | Ponte B |
| GET/POST `/api/projects/{projectId}/method/repositories` | GET GitRepositoryDto[]; POST RegisterGitRepositoryRequest → GitRepositoryDto, admin UiSession, App/instalação validadas | Ponte B |
| PUT `/api/projects/{projectId}/method/repositories/{integrationId}` | UpdateGitRepositoryRequest → GitRepositoryDto; preserva campos opcionais não enviados, não troca tenant/repo/principal | Ponte B |
| GET `/api/projects/{projectId}/method/pull-requests?linked=false&page=1&pageSize=25` | GitPullRequestPage, escopo/permissões existentes | Ponte B |
| POST `/api/projects/{projectId}/method/pull-requests/{observationId}/link` | LinkObservedPullRequestRequest → TaskGitLinkDto; link e projeções atômicos, reavalia fato atual sem contrariar intervenção humana | Ponte B |
| POST `/api/integrations/git/github/{integrationId}/webhook` | Bytes originais/HMAC, header Delivery/Event;202 durável,200 repetido,409 hash divergente; DTO normalizado nunca é body de entrada externo | Ponte B |

O tipo enum PlatformPermission em Permissions usa os valores numéricos existentes do catálogo, não um catálogo novo. A UI (e qualquer cliente desta rota) usa o GUID workItemId retornado por MethodTaskDto para resume-approval; as oito ferramentas MCP continuam identificando tasks por T-XXXX, sem nova ferramenta de liberação humana. Datas UTC ISO8601 e GUIDs canônicos. Valores byte[] internos são base64 quando expostos como JSON; credencial opaca nunca é campo de metadados. `CreateMethodTaskRequest` usa campos id/title/hu/filePath/covers/risk/status; o MCP preserva sua interface pt-BR e traduz os campos. `MethodTaskDto.workItemId` é o GUID; `id` é T-XXXX. `authorized` prova concessão do ciclo, não só nome da coluna. `canResume` é específico do principal/bloqueio/aprovação atuais. `cycleId`, `blockingEventId`, `blockingPrincipalId` e `blockingOriginStatus` são metadados de leitura da task para a UI identificar/liberar o bloqueio vigente; a API revalida todos na transação, não os aceita como autoridade do cliente. `project.fields` mapeia os seis nomes estáveis (ID do metodo/HU/Arquivo/Cobre/Risco/PR) para GUIDs, preenchidos na resposta real; os três IDs dedicados permanecem compatíveis.

MCP: os8 nomes existentes são preservados. Fluxo de startup: config token opaco → GET global me → comparar PRISMA_PROJECT (nome/ID) e PRISMA_BOARD se explicitamente informado (nome/ID) → GET project rules. PRISMA_ROLE, se presente, só compara a expectativa. Leituras não usam login, refresh nem criação/ajuste de estrutura.401/403 manda parar e avisar, sem segunda tentativa humana.

## Persistência, prova e transação

Interfaces em Application/Interfaces; records em Features/MethodGit/Contracts. Nenhuma interface exige entidades de A/B ainda não entregues. B pode desenvolver com fake de IMethodGitStore/IGitProvider/IMethodMoveService; F2 prova os serviços reais.

- A política preserva a validação existente de portal, seed e automações legadas em projetos sem método habilitado, inclusive quando não existe contexto de autenticação do método. Em projeto com método, essa origem pode criar backlog, mas não iniciar execução nem conceder liberação de retomada. Contexto desconhecido com principal/token não ganha autoridade interativa.
- Token humano continua vinculado à própria pessoa, mas a concessão de execução exige UiSession também ao criar ou transferir de outro quadro para a coluna de desenvolvimento do método. O retorno revisão → desenvolvimento por token exige autorização vigente e origem/destino no mesmo quadro do método. Permissões humanas UI continuam fora da matriz.
- O avaliador de delegação é intencionalmente mais restritivo que as permissões humanas: exige projeto ativo/e-mail confirmado e propaga negativas de projeto aos itens; nunca amplia direitos. Teste de paridade cobre View/Create/Edit/ChangeStatus/Comment e negativa explícita no projeto, sem mudar o PermissionService da UI. Casos específicos de item e HTTP/SQL são verificados na integração F1/F2.
- GitRepositoryDescriptor.OperationalError é projeção opcional de estado operacional, com mensagens fixas sem credenciais/corpos/PEM. Não altera autoridade nem substitui a validação da configuração no worker.

- NormalizedGitEvent contém apenas os dados mínimos para retry. InstallationId é opcional: webhook por repositório pode não enviá-lo. Cadastro valida App/instalação; se payload informar installation, precisa coincidir. Ausência não é rejeição automática do webhook de repositório.
- GitPullRequestSnapshot.closedAtUtc preserva closed_at do provedor, separado de updatedAtUtc; leitura state=all confirma causalidade de merge retido frente a fechamento posterior sem merge (PD-06), sem tomar edição posterior por fechamento.
- NormalizedGitEvent/GitPullRequestSnapshot.HeadRepositoryId preserva head.repo.id confirmado pelo webhook/provedor. Ausência significa origem desconhecida; não presume o repositório base. B só infere task por branch/título quando esse ID corresponde ao repositório configurado; fork exige vínculo explícito autorizado.
- GitPullRequestSnapshot.PullRequestAuthorId/PullRequestAuthorName opcionais registram autor Git observado no vínculo, separado do agente/sessão que o cadastrou; são metadados, nunca autorização por personificação.
- IGitProvider.ListPullRequestsForCycleAsync limita o histórico ao ciclo autorizado e mantém todos os PRs abertos; o resultado Complete ainda exige paginação comprovada. Implementação padrão delega a lista completa para compatibilidade dos fakes, sem fabricar completude. GetPullRequestObservationAsync busca pontualmente no tenant/projeto; o padrão null não concede vínculo.
- Rotação HMAC é responsabilidade do chamador do provedor: tentar segredo anterior somente com PreviousSecretValidUntilUtc > agora, dentro da janela24h configurada. Registrar current/previous no resultado operacional/métrica, sem registrar segredo ou referência sensível; assinatura inválida continua recusada, inclusive para delivery repetido.
- Provider valida instalação/repo e lê PR/reviews/PRs abertos com completude explícita. Falha/paginação parcial impede promoção por merge. Credencial real é GitHub App; nenhum PAT/login de usuário. Segredos resolvidos somente no namespace externo MethodGit:Secrets, sem buscar variável/arquivo arbitrário por referência do cliente.
- ClaimNextDelivery pega só IDs/metadados e lease/rowversion. Worker abre escopo com IMethodWorkerContextInitializer antes de carregar entrega/task. CompleteDelivery é compare-and-swap; worker que perdeu lease não termina o trabalho do novo dono.
- IMethodMoveService é implementação única Forja/F1. Persistência de StageId/StageHistory/TaskEvent/MethodAuthorizationEvents/GitAppliedFacts/consumo da entrega é atômica. Só publica notificação depois do commit. `MethodMoveRequest.expectedRowVersion` é a versão da task; expectedDeliveryRowVersion a lease da entrega. Webhook não chama Move genérico e marca entrega separadamente.
- ObservedGitActorId/ObservedGitActorName opcionais no evento normalizado/fato preservam sender.id/login separados de ator sistema. F1 usa gitActorId/gitActorName no payload; não concedem autoridade nem substituem o principal. Reavaliação sem evento mantém null.
- MethodTaskSnapshot.LastManualChangeAtUtc é o último movimento manual (UiSession ou MethodToken), excluindo comentários/edições comuns e IntegrationWorker. Política compara o fato ao máximo deste valor e LastInteractiveChangeAtUtc, para não desfazer retorno manual por token humano no mesmo ciclo. É derivado do histórico, sem novo status ou coluna.
- VerifiedGitFact é interno, criado após validação do provedor; nunca aceitar de DTO HTTP/body genérico. CycleId corresponde ao ciclo comprovado pelo fato original, não o ciclo atual atribuído a replay antigo. Timestamp do fato, concessão do ciclo, intervenção humana posterior, origem canônica e fatos consumidos são revalidados na transação.
- SemanticKey é SHA256 de identidade canônica do fato (repo/PR ou review, revisão, tipo, ciclo original), não só destino. Preserva reabertura legítima e bloqueia replay após expurgo operacional.
- Link explícito usa operação atômica própria do store B para tabela/projeçãoPR/comentário. Depois reavalia estado confirmado com a App; não toma marcador livre como prova. Novo vínculo nunca concede autorização humana.

## Donos

Forja F0: este documento; Domain/Enums/MethodEnums.cs; Application/Features/MethodGit/Contracts/MethodContracts.cs e GitContracts.cs; interfaces IMethodAccessContext, IMethodContextInitializer (inclui worker), IMethodTransitionPolicy, IMethodMoveService, IMethodGitStore, IGitProvider (inclui secret store); contexto/roadmap/spec/PROGRESS/NOTAS. A/F1/UI-C e todos os existentes compartilhados permanecem Forja. Novos controllers de identidade/settings/tasks e provisionamento são Forja, além de MethodTokensController.

Ponte B: entidades/configuraçãoMethodGit, serviço/worker/provedor/storesGit e três controllersGit. Também TODO tools/prisma-mcp/src/**. Prova: testes adversariais MetodoGit/**, fixtures/E2E da spec, tools/prisma-mcp/test/**, README/DEMO e QA. Testes A/F0 e de guard genérico ficam Forja; testes B ficam Ponte. Não alterar arquivo do outro nem para corrigir compilação; reportar em ESTADO.

## Checkpoints

Após F0 compilado e publicado, Ponte anuncia assinaturas em ESTADO antes de remover login de src; Prova troca testes no mesmo checkpoint. Não declarar entrega MCP com testes antigos vermelhos. Git por arquivo e pull de feat antes de commit, sem add-A; atualizações de main entram por merge sem rebase/force push. Nunca push main, merge PR ou deploy por executor.


## Inventário nominal para F1 e testes de guard

Todos os arquivos existentes desta tabela pertencem à Forja. Os nomes de casos são o compromisso de cobertura de `MethodGenericWriteGuardTests`, não testes já executados no F0. Cada caso inclui token restrito e contraponto UiSession com permissões atuais. Última barreira EF deve impedir caminhos futuros não listados; a tabela é atualizada se a inspeção revelar novo setter/SQL.

| Entrada/chamador | Implementação compartilhada | Caso nominal F1 |
|---|---|---|
| WorkItemsController.Create | CreateWorkItemCommandHandler | CreateCannotStartAgentExecution |
| WorkItemsController.Move | MoveWorkItemCommandHandler | DirectMoveCannotAuthorize |
| WorkItemsController.Update/Duplicate e custom fields | WorkItemManagementFeature | UpdateDuplicateAndIdentityCannotBypass |
| WorkItemDetailsController dados/taxonomia | WorkItemDetailsFeature | DetailsCannotEscapeMethodScope |
| ProductivityController.Bulk | ProductivityFeature | BulkPreservesInitiatingActor |
| IAutomationExecutor disparado por handlers | AutomationExecutor | AutomationDoesNotElevateAgent |
| BoardTransfersController e BoardsController exclusão | BoardStructureFeature, BoardStructureRepository, DeleteBoardCommandHandler | TransferDeleteAndRemapCannotAuthorize |
| ExternalPortalController.Triage e criação externa | ExternalRequestTriageFeature, ExternalPortalFeature, ExternalFormsFeature | ExternalRoutingDoesNotFabricateHumanAuthority |
| SprintsController.ChangeStatus | SprintsFeature | SprintSideEffectsKeepOriginalActor |
| AiController e aplicação de sugestões se disponível | Features/Ai, Services/Ai/AiService | AiApplicationKeepsInitiatorAndD91 |
| Repositórios e SaveChanges[Async] | AppDbContext e WorkItem/custom fields repositories | PersistenceGuardCatchesIndirectWrites |
| AuthController login/refresh | Autenticação/middleware | MethodCredentialCannotMintUiSession |
| Filtros/escopos genéricos | ProjectAccessService, PermissionService, WorkItemAccessService, BoardAccessService, OrganizationContextMiddleware | PrincipalCannotBorrowIssuerOrChangeScope |
| Auditoria HTTP e worker | AuditContextMiddleware, IMethodWorkerContextInitializer, IMethodMoveService | WorkerHistoryAndAuditAreAtomic |

Leituras de autor, baixo2 de git-spec-2: `Infrastructure/Identity/UserDirectory.cs` (nomes do tenant), `Infrastructure/Repositories/WorkItemDetailsRepository.cs` (detalhe/comentários), consultas de histórico/Grafo em `Application/Features/WorkItems/WorkItemManagementFeature.cs`, `Application/Features/Productivity/ProductivityFeature.cs` (atividades) e `Application/Features/Reports/{ReportsFeature,PreparedReportsAndDashboardsFeature,OrganizationHoursReportFeature,ReportBuilderFeature}.cs` (produtividade/horas). F1 usa `MethodPrincipals.DisplayName` no tenant e snapshots do histórico; não exige usuário Identity sintético nem altera seletor de membros humanos. Casos em `MethodPrincipalPresentationTests`: comentário, StageHistory/TaskEvent, atividades e relatório com `method:<guid>` mostram o nome e preservam legado; principal de outro tenant não é exposto.


## Limites da integração F1 e revisão git-8

- A flag global só habilita a descoberta de escopo. SaveChanges de principal do Método ou gravação que alcance projeto provisionado recebe guard e transação Serializable; gravações comuns em outros projetos conservam o caminho e o isolamento anteriores. A descoberta usa IDs do lote e consultas em conjunto. Projetos pausados conservam o recorder de aposentadoria de provas: movimento humano durante a pausa não deixa concessão antiga reutilizável na reativação. Escrita mista verifica cada projeto antes de validar coluna; coluna legada sem BoardId fora do Método continua editável. Automações e sincronização de workflow também selecionam o projeto antes de usar a persistência do Método; outros projetos mantêm um Save ao final da cadeia e o ExecuteUpdate legado.
- Entidades WorkItem do Método devem ser carregadas rastreadas antes de mover: GetById/GetForMove, incluindo a operação em lote, mantêm o StageId/BoardId original. Attach de uma entidade destacada com destino já preenchido não contém a origem confiável e recebe409; não é uma API alternativa de movimento.
- O teto da UiSession é metadado de contexto, nunca autorização. PermissionService e ProjectAccessService conservam a autoridade humana existente e consultam suas permissões reais. Só tokens do Método e worker passam pela matriz.
- O hub reconhece MethodToken pela identidade autenticada da conexão, não pelo contexto scoped do middleware: SignalR abre novo escopo por invocação. Cada JoinBoard revalida token/principal/delegação/prazo/revogação/flag e confere projeto/tenant; o token humano não usa o fallback de associação humana. O ID do token é metadado interno da identidade autenticada, não um endpoint de emissão ou credencial substituta. JWT humano conserva o caminho anterior.
- O serviço recusa a autenticação de token quando a flag atual é desligada depois do startup. Habilitar após um startup desligado continua exigindo reinício para registrar o handler; desligar retira acesso imediatamente no próximo pedido/JoinBoard.
- Avaliação do baixo4: o teto do token humano continua sendo a interseção explícita com as permissões atuais da própria pessoa, validade padrão30dias/máxima90 e rotação sem ampliação. Reduzir por padrão ManagePermissions/Delete na emissão e adicionar a seleção explícita na UI fica como melhoria baixa; não altera o contrato aprovado nem empresta autoridade do emissor.


## Correção do QA P1: CAS de vínculo e guard de autoridade

O store marca MethodTaskId como modificado com o mesmo valor para atualizar RowVersion ao vincular PR/branch. Esse pulso não altera identidade, concessão, bloqueio ou status. O guard aceita somente um MethodTaskState existente no tenant/projeto do principal, com apenas esse campo marcado e o valor igual ao OriginalValue. Releitura sem tracking compara também com o registro real: ID, ciclo, concessão, bloqueio/principal, liberação e último fato precisam ser idênticos; RowVersion atual e original precisam coincidir com o banco, senão409. Exige Edit efetivo sobre a tarefa. OriginalValue forjado não é prova. State novo, alteração real, campos extras marcados, provas e auditoria continuam recusados. Nada concede execução por esse caminho; a matriz e a liberação UI permanecem exigidas.
