# SPEC-TIME-TRACKING: Apontamento de horas e justificativas diárias

**Status:** approved
**Baseline:** contrato aprovado em 2026-08-24, com revisão expressamente autorizada pelo PO na tarefa 7 de 2026-10-15. A revisão abaixo prevalece nos pontos indicados.

## Revisão aprovada em 2026-10-16 — Frente 1, HORAS-B07

- Decisão do PO em `GOALS.md`, Frente 1/1b: ratear todos os intervalos nas fronteiras de dia de **America/Sao_Paulo**, tanto encerrados quanto abertos até o instante de cálculo. Semana, diário por tarefa, consultas de lançamentos do dia, relatórios e somas para validações usam somente a interseção do período com o dia/recorte solicitado. Um período de 22:00 a 02:00 contribui 2h para cada dia; não criar dois registros físicos nem duplicar a duração total.
- Consultas por período incluem lançamentos iniciados antes do recorte e ainda não encerrados ou encerrados dentro dele. Precisão em segundos inteiros; a soma das parcelas conserva os segundos do intervalo, inclusive nas fronteiras com frações de segundo.
- Somente o dono altera/exclui seus lançamentos. Usuário comum vê apenas suas horas no relatório organizacional, inclusive quando pede usuário/equipe de terceiro; administrador vê o tenant ativo e gestor somente as equipes que lidera, respeitando negações explícitas. Autorização ocorre antes da agregação.
- O mesmo recorte de `specs/dashboards-reports.md:101–122` vale para relatórios preparados, construtor, relatórios salvos/CSV, dashboards, métricas do quadro, Empresa e horas semanais em Equipes. Compartilhar uma definição não transfere as permissões do autor. O acesso de leitura ao projeto/quadro não concede acesso às horas de terceiros; listas de lançamentos e totais de cartões, detalhes e subtarefas preservam essa restrição.
- Nas superfícies pessoais, o próprio dono continua vendo seu histórico, inclusive se for gestor e tiver lançado em tarefa sem equipe liderada. Horas adicionais de terceiros exigem papel administrador ou equipe do item/quadro liderada pelo gestor. Relatórios gerenciais usam somente sua população autorizada, sem acrescentar horas próprias fora dela. Negação explícita de relatório remove dados de terceiros, sem impedir a consulta do histórico pessoal; os portões de acesso ao recurso continuam obrigatórios. Esta aplicação do B07 foi encaminhada nominalmente pelo Maestro em 2026-10-16.
- Criação manual e edição compartilham validação de início/fim, futuro, tamanho da observação e sobreposição. Legados já sobrepostos continuam corrigíveis quando o novo intervalo não aumenta a interseção com nenhum outro lançamento, inclusive ao deslocar horários para reduzir um conflito. Novos conflitos ou aumento de conflito existente são recusados; um dia legado acima de 24h só aceita correção que não aumenta sua parcela.
- O ajuste rápido escolhe intervalo livre, preferencialmente após 09:00 e, se necessário, em outra lacuna passada do mesmo dia; nunca fixa todos os ajustes às 09:00 nem cria período futuro. Sem espaço suficiente, informa erro e não grava.
- As validações diárias consideram até 24h por dia de Brasília, somando parcelas, não a duração integral de um período iniciado na véspera. Não acrescentar horas a um dia legado acima desse limite; uma correção que mantém ou reduz o intervalo legado continua possível. O painel Meu Tempo deve permitir acessar os sete dias por toque no mobile, inclusive a primeira e a última coluna.
- Limite/encerramento automático de cronômetro esquecido permanece para decisão futura do PO; esta revisão não corta a duração real. Nenhuma mudança de schema ou de estrutura de auditoria prevista.

## Revisão aprovada em 2026-10-15 — correção de horas (tarefa 7)

- Totais de cartões incluem o timer aberto até `timeCalculatedAt`, calculado uma única vez por resposta. O navegador soma somente o decorrido após esse instante (ou após o início, quando posterior). `GET /api/me/work` continua retornando somente horas encerradas em `userTimeSeconds`; Meu trabalho soma o intervalo ativo uma vez. Painel/drawer usam intervalos individuais sem acrescentar a duração aberta duas vezes.
- `PUT /api/timeentries/{id}` recebe `startedAt`, `endedAt` e `note` (até 2000 caracteres); retorna o lançamento atualizado (200). `DELETE /api/timeentries/{id}` retorna 204.
- Somente o dono, autenticado e com acesso de edição à tarefa na organização corrente, pode corrigir/excluir. Não existe permissão administrativa específica implementada para horas de terceiros: permanece lacuna, sem usar permissões administrativas genéricas como autorização implícita.
- Timer aberto não pode ser editado ou excluído: deve ser parado primeiro. Edição exige fim maior que início, nenhum instante futuro e ausência de sobreposição com outro lançamento do mesmo usuário; o próprio registro é desconsiderado. Erros temporais/de regra retornam 400, terceiro 403, recurso inexistente/inacessível no tenant 404. Não há minutos negativos.
- Alteração/exclusão reutilizam a auditoria imutável de `AppDbContext` (D34), na mesma gravação: ator autenticado, valores anteriores/novos alterados, origem e correlação. Exclusão mantém snapshot anterior e novo nulo. Sem mudança de estrutura de auditoria.
- `GET /api/me/time/entries?date=yyyy-MM-dd` lista somente os lançamentos próprios que interceptam o dia de Brasília, com tarefa, horários originais, duração, observação e `isManual`. Um lançamento atravessando meia-noite aparece nos dois dias, com o mesmo ID; diário por tarefa e semana rateiam sua contribuição sem duplicar o total.
- No modal Ajustar tarefas: listar intervalos, editar início/fim/observação e excluir com confirmação na própria UI; atualizar total diário, semana e cartões após mutações. Horários do editor são explicitamente de Brasília, independentemente do fuso do navegador.
- `GET /api/me/time/tasks?query=` busca título ou número, somente tarefas e projetos não arquivados em projetos acessíveis; atribuídas ao usuário primeiro, até 50 resultados. Campo operável por teclado e mobile; sem requisição por quadro.
- **Atualização de 2026-10-16:** a decisão de rateio acima substitui a atribuição integral ao dia de início da tarefa 7. Exemplo: 14/10 12:44 até 15/10 07:12 soma 11h16 em 14/10 e 7h12 em 15/10, preservando 18h28 no total. Limite para cronômetro esquecido permanece pendente.
- A precisão continua em segundos inteiros, sem arredondamento de negócio. Nenhuma migration necessária nesta implementação.

## Propósito

Definir o contrato funcional do apontamento de horas baseado em `TimeEntry`, documentar as justificativas diárias existentes em `DayJustification` e explicitar o que já está implementado e o que ainda precisa ser adequado e homologado.

## Contexto

O sistema possui cronômetro, lançamento manual, consultas e totalizações de horas. Durante a revisão humana, PO definiu as regras oficiais de precisão, jornada diária, sobreposição, autorização, timezone e operação em tarefas concluídas. Essas decisões têm precedência sobre o comportamento legado descrito anteriormente como baseline as-built.

## Escopo incluído

- Início e encerramento de cronômetro em uma tarefa.
- Lançamento manual com data/hora inicial e final.
- Consulta e totalização de apontamentos por tarefa, usuário e quadro, enquanto esse recorte existir no modelo.
- Visões diárias e semanais de horas.
- Alteração e exclusão de apontamentos conforme autorização definida nesta spec.
- Registro de horas em tarefas abertas ou concluídas.
- Justificativas diárias já existentes, sem criar novas regras de negócio não decididas.

## Escopo excluído

- Folha de pagamento, banco de horas legal, salário, custo, faturamento ou rentabilidade, conforme D30.
- Aprovação gerencial de horas.
- Arredondamento para blocos de 5, 10 ou 15 minutos.
- Fechamento mensal e bloqueio retroativo não definidos nesta revisão.
- Novas regras para conteúdo, unicidade ou aprovação de `DayJustification`.

## Contrato funcional aprovado

### Registro

- As horas podem ser registradas por cronômetro e por lançamento manual.
- Todo apontamento pertence a um usuário identificado e a uma tarefa acessível.
- É permitido lançar e corrigir horas em tarefas concluídas.
- Um lançamento manual exige `EndedAt > StartedAt`.
- O tempo é apurado pela diferença exata entre início e fim, sem arredondamento para blocos de minutos.

### Cronômetro único

- Cada usuário pode ter somente um cronômetro aberto por vez.
- Iniciar um cronômetro em outra tarefa encerra automaticamente o cronômetro anterior e inicia o novo.
- Iniciar novamente na mesma tarefa não deve criar um segundo cronômetro aberto.

### Sobreposição e limite diário

- O mesmo usuário não pode possuir períodos de trabalho sobrepostos, independentemente de terem sido criados manualmente ou pelo cronômetro.
- Rateio à meia-noite é obrigatório conforme decisão de 2026-10-16. Qualquer cálculo de limite diário deve usar a parcela do período no dia de Brasília, sem impor limite à duração total de um cronômetro esquecido.
- Dias, relatórios e agrupamentos usam o horário de Brasília.

### Alteração, exclusão e aprovação

- O proprietário do apontamento pode alterar ou excluir seus próprios registros.
- A autorização administrativa específica para horas de terceiros permanece lacuna; nesta entrega somente o proprietário pode alterar/excluir.
- Usuários comuns não podem alterar ou excluir apontamentos de terceiros.
- Apontamentos não exigem aprovação para serem contabilizados.
- Alterar um apontamento reaplica as validações compartilhadas de intervalo, futuro, sobreposição, limite diário, usuário, tarefa e autorização, preservando a possibilidade de reduzir um intervalo legado já inconsistente.

### Precisão e timezone

- A fonte de verdade é o intervalo `StartedAt`/`EndedAt`.
- A duração deve ser calculada a partir desses timestamps, sem arredondamento de negócio.
- Persistência e transporte continuam usando `DateTimeOffset`; apresentação e recortes diários usam Brasília, repartindo os períodos na meia-noite local.

## Atores e permissões

- **Usuário autenticado:** inicia e encerra seu cronômetro, cria lançamentos manuais e consulta dados permitidos.
- **Proprietário do apontamento:** altera e exclui seus próprios registros.
- **Administrador:** não recebe autorização implícita para horas de terceiros; permissão específica permanece pendente.
- **Usuário comum:** não altera nem exclui registros de terceiros.
- **Usuário anônimo:** não acessa operações protegidas.

## Baseline anterior à correção de 2026-10-15 (histórico)

- `TimeEntry` persiste `WorkItemId`, `UserId`, `StartedAt`, `EndedAt`, `Note`, `IsManual` e `CreatedAt`.
- `TimeEntry.IniciarAgora` abre um cronômetro usando `DateTimeOffset.UtcNow`.
- `TimeEntry.Manual` cria lançamento manual e rejeita fim menor ou igual ao início.
- `TimeEntry.Encerrar` encerra um cronômetro aberto usando `DateTimeOffset.UtcNow`.
- `DurationSeconds` calcula a duração em segundos inteiros a partir de `StartedAt` e `EndedAt`; não existe arredondamento para blocos de minutos.
- `StartTimerCommandHandler` procura o cronômetro aberto do usuário, devolve o mesmo registro quando ele já pertence à tarefa solicitada e encerra o anterior antes de iniciar outro em tarefa diferente.
- `CreateManualTimeEntryCommandHandler` permite lançamento manual após validar acesso de edição à tarefa.
- Os handlers de início e lançamento manual não bloqueiam explicitamente tarefas concluídas; portanto, o fluxo atual é compatível com lançamento em tarefa concluída, sujeito a teste de integração e homologação.
- `TimeEntriesController` exige autenticação e expõe consulta do timer atual, consulta por tarefa, início, encerramento, lançamento manual e totais por tarefa, usuário e quadro, além da visão semanal do usuário.
- A visão semanal usa deslocamento fixo UTC-03:00 para agrupar por dia; isso coincide com o horário atual de Brasília, mas não centraliza uma política institucional de timezone.
- `DayJustification` existe como recurso separado, isolado por organização, com inclusão, consulta e exclusão próprias.
- Não há estado ou fluxo de aprovação de `TimeEntry`, compatível com a decisão de não exigir aprovação.

## Gaps identificados na baseline anterior

1. **Alterar e excluir `TimeEntry`:** resolvido para o proprietário na tarefa 7; autorização administrativa específica permanece pendente.
2. **Sobreposição:** o lançamento manual não consulta intervalos existentes do usuário e pode sobrepor outro lançamento ou um cronômetro aberto.
3. **Limite de 24 horas por dia:** não existe validação consolidada do total diário, incluindo a divisão de períodos que atravessam a meia-noite.
4. **Timezone diário/semanal:** resolvido na tarefa 7 pela política compartilhada `TimeTrackingDay` / `America/Sao_Paulo`.
5. **Administrador:** falta comprovar e implementar a permissão administrativa específica para alteração e exclusão de horas de terceiros sem permitir acesso horizontal indevido.
6. **Tarefa concluída:** o código não contém bloqueio explícito, mas faltam testes que comprovem criar, alterar e excluir horas em tarefa concluída.
7. **Concorrência:** a regra de cronômetro único é aplicada no handler, porém não foi comprovada uma constraint ou estratégia transacional que impeça dois cronômetros abertos em requisições simultâneas.
8. **Testes:** não há cobertura encontrada para sobreposição, limite diário, timezone uniforme, alteração/exclusão, autorização de administrador e tarefa concluída.

## Regras de `DayJustification`

- Toda justificativa pertence a uma organização, um usuário e uma data.
- O recurso permanece separado dos apontamentos de tarefa.
- As decisões desta revisão sobre edição/exclusão de `TimeEntry` não alteram automaticamente as permissões de `DayJustification`.
- Conteúdo obrigatório, unicidade por usuário/data e limites de horas continuam conforme a implementação atual até revisão específica.

## Estados operacionais

### TimeEntry

- **Em andamento:** `EndedAt` é nulo.
- **Encerrado:** `EndedAt` está preenchido e a duração pode ser totalizada.
- **Alterado:** início, fim ou observação foram corrigidos pelo proprietário autorizado.
- **Excluído:** registro removido do fluxo de consulta pelo proprietário autorizado; auditoria preservada.
- **Rejeitado:** validação temporal, sobreposição, limite diário, acesso ou referência impede a operação.

Esses estados descrevem o ciclo operacional; não exigem a criação de um enum persistido.

## Persistência

- `TimeEntry` é a fonte de verdade dos apontamentos e mantém timestamps com offset.
- `DurationSeconds` é derivado do intervalo e não representa uma coluna independente.
- `IsManual` diferencia lançamento manual de cronômetro.
- `DayJustification` implementa `IOrganizationOwned` e permanece isolado por organização.
- A tarefa 7 autorizou migration se necessária para esta correção; nenhuma foi necessária. Constraints futuras exigem análise e autorização próprias.

## API atual

- `GET /api/timeentries/running`
- `GET /api/timeentries/work-item/{workItemId}`
- `POST /api/timeentries/start`
- `POST /api/timeentries/stop`
- `POST /api/timeentries/manual`
- `GET /api/timeentries/work-item/{workItemId}/total`
- `GET /api/timeentries/user/{userId}/total`
- `GET /api/timeentries/board/{boardId}/total`
- `GET /api/timeentries/my/weekly`
- Endpoints de `MeTimeController` para consulta diária e justificativas.

Contratos de PUT/DELETE e consultas para ajuste definidos na revisão de 2026-10-15 acima.

## Interface

- Deve oferecer cronômetro e lançamento manual.
- No detalhe da tarefa, a visão principal consolida o tempo por responsável e informa o primeiro início, o
  último fim ou estado em andamento e o total acumulado de cada pessoa.
- Cada intervalo individual, sua observação e duração permanecem disponíveis no `Log de apontamentos`,
  recolhido por padrão para não competir com o resumo operacional.
- Deve permitir que o proprietário altere e exclua seus apontamentos. Administração de horas de terceiros permanece pendente.
- Deve permitir apontamentos em tarefas concluídas.
- Deve comunicar claramente conflitos de sobreposição e limite diário, sem expor registros de terceiros.
- Deve exibir tempos sem arredondamento para blocos de minutos.
- Datas e agrupamentos diários devem seguir o horário de Brasília.
- Não deve apresentar aprovação de horas como etapa obrigatória.

## Validações e erros

- Rejeitar fim menor ou igual ao início.
- Rejeitar período que sobreponha qualquer apontamento existente do mesmo usuário.
- Aplicar rateio por dia de Brasília nos cálculos; não impor corte automático a cronômetro esquecido.
- Ao alterar, desconsiderar o próprio registro na busca por conflito e revalidar o intervalo completo.
- Rejeitar usuário ou tarefa inexistente/inacessível.
- Rejeitar alteração ou exclusão de terceiro sem permissão administrativa adequada.
- Não bloquear um lançamento somente porque a tarefa está concluída.
- Erros não devem expor dados de apontamentos de outros usuários.

## Evidência operacional complementar — revisão de 2026-10-16

- O recorte atual de equipes nas horas usa a equipe do item (`WorkItem.TeamId`) ou, na ausência dela, a do quadro (`Board.TeamId`). Não usa a equipe cadastral de quem apontou: o gestor pode ver horas de outra pessoa em item da equipe gerenciada, mas não recebe horas dessa pessoa em item de outra equipe.
- A validação compartilhada de períodos cobre criação manual e edição. Iniciar o cronômetro representa o instante atual e ainda não passa por essa validação: pode iniciar sobre um ajuste manual preexistente. É uma limitação atual; a edição do legado apenas mantém/reduz conflitos. Não representa autorização para novos apontamentos manuais sobrepostos nem resolução das corridas concorrentes.
- A proteção B07 inclui o construtor: prévia, execução de definição compartilhada e exportação usam o papel de quem executa, nunca o de quem criou a definição. Linhas de apontamento e de usuário, métricas e horas embutidas nas outras fontes seguem o mesmo recorte; ausência de filtro de projeto não concede acesso a outros projetos.

## Segurança

- A identidade do proprietário deve vir do contexto autenticado, não de um `UserId` arbitrário enviado pelo cliente.
- Consultas e mutações devem respeitar organização, tarefa e permissões.
- A autorização administrativa não pode atravessar organizações.
- Correções e exclusões devem ser auditáveis pelas estruturas transversais existentes, sem registrar segredos.

## Critérios de aceite

- **Dado** um usuário sem cronômetro ativo, **quando** iniciar o cronômetro em uma tarefa acessível, **então** um único apontamento em andamento é criado.
- **Dado** um cronômetro ativo em outra tarefa, **quando** o usuário iniciar um novo, **então** o anterior é encerrado e o novo é iniciado.
- **Dado** um período manual válido e sem conflito, **quando** o usuário lançá-lo, **então** a duração exata é contabilizada sem arredondamento por blocos.
- **Dado** qualquer apontamento sobreposto do mesmo usuário, **quando** houver criação ou alteração conflitante, **então** a operação é rejeitada sem persistência parcial.
- **Dado** um período 22:00–02:00 em Brasília, **quando** consultar cada dia ou o relatório/semana correspondente, **então** cada dia contém 2h e o conjunto conserva 4h, sem dividir o registro persistido.
- **Dado** um apontamento próprio, **quando** o proprietário o alterar ou excluir, **então** a operação é permitida e os totais são atualizados.
- **Pendente:** autorização administrativa específica para alterar/excluir horas de terceiros.
- **Dado** um usuário comum, **quando** tentar alterar ou excluir apontamento de terceiro, **então** a operação é negada sem vazamento de dados.
- **Dado** uma tarefa concluída e acessível, **quando** houver lançamento ou correção de horas, **então** a operação segue as mesmas regras de uma tarefa aberta.
- **Dado** um apontamento válido, **quando** for persistido, **então** ele é contabilizado imediatamente, sem aprovação.
- **Dado** vários apontamentos da mesma pessoa na tarefa, **quando** o detalhe é aberto, **então** a visão
  principal apresenta uma linha consolidada e o log preserva cada intervalo individual.
- **Dado** um relatório ou agrupamento diário, **quando** os registros forem projetados, **então** os limites de dia seguem o horário de Brasília.

## Testes e evidências exigidos

- Unidade: intervalo válido/inválido, duração sem arredondamento por blocos e encerramento do cronômetro.
- Aplicação: timer único, idempotência na mesma tarefa, encerramento automático ao trocar de tarefa, sobreposição e limite diário.
- Autorização: proprietário, administrador autorizado, usuário comum e isolamento entre organizações.
- Integração: criar, alterar, excluir e totalizar registros; intervalos na virada do dia de Brasília; concorrência de início de timer.
- E2E: cronômetro, lançamento manual, correção/exclusão própria, tarefa concluída e mensagens de conflito.
- Evidência atual localizada: `TimeEntry.cs`, handlers e queries de `Features/TimeEntries`, `TimeEntriesController`, `TimeEntryRepository`, `GetMyDailyByTaskQueryHandler` e `GetMyWeeklyTimeQueryHandler`.

## Homologação manual pendente

- Iniciar, acompanhar e encerrar um cronômetro.
- Trocar o cronômetro entre duas tarefas e confirmar o encerramento automático do anterior.
- Criar um lançamento manual e conferir o total sem arredondamento por blocos.
- Tentar lançar períodos sobrepostos e ultrapassar 24 horas no mesmo dia.
- Alterar e excluir apontamento próprio e validar a operação administrativa em outro usuário.
- Lançar e corrigir horas em tarefa concluída.
- Conferir agrupamentos na virada do dia segundo o horário de Brasília.

Os cenários ligados aos gaps devem permanecer como não homologáveis até a respectiva implementação.

## Dependências

- Identidade e autenticação do usuário.
- Autorização multitenant e acesso à tarefa.
- `WorkItem`, `TimeEntry` e `DayJustification`.
- Features MediatR e persistência EF Core/SQL Server.
- Auditoria transversal para mutações relevantes.

## Riscos

- Sobreposição e concorrência podem inflar totais enquanto os gaps não forem corrigidos.
- Agrupamentos podem divergir na virada do dia por uso inconsistente de UTC e horário de Brasília.
- Correção/exclusão estão disponíveis para o dono; a permissão administrativa específica permanece pendente.
- Sem teste específico, lançamento em tarefa concluída pode regredir silenciosamente.

## Decisões pendentes

- Política de fechamento mensal ou bloqueio retroativo, caso o produto venha a exigir.
- Regras específicas de conteúdo e unicidade das justificativas diárias.
- Permissão administrativa específica para horas de terceiros.
- Limite para cronômetro esquecido; rateio à meia-noite foi decidido em 2026-10-16.

## Rollback

Reverter o código da tarefa 7 remove os novos endpoints e a interface de correção, sem rollback de schema. Lançamentos já corrigidos/excluídos permanecem auditados; restaurar valores exige ação autorizada a partir da auditoria. Não há migration.

## Referências

- `src/Prisma.Workspace.Domain/Entities/TimeEntry.cs`
- `src/Prisma.Workspace.Domain/Entities/DayJustification.cs`
- `src/Prisma.Workspace.Application/Features/TimeEntries/`
- `src/Prisma.Workspace.Application/Features/MeTime/`
- `src/Prisma.Workspace.Infrastructure/Repositories/TimeEntryRepository.cs`
- `src/Prisma.Workspace.Api/Controllers/TimeEntriesController.cs`
- D30, D34, D40 e decisões explícitas de PO em 2026-08-24.

## Rastreabilidade

Decisões humanas de apontamento → `SPEC-TIME-TRACKING` → gaps de implementação → testes automatizados → homologação manual por módulo.
