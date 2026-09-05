# SPEC-TIME-TRACKING: Apontamento de horas e justificativas diárias

**Status:** approved
**Baseline:** contrato revisado e aprovado por PO em 2026-08-24; comportamento atual e gaps estão separados abaixo.

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
- Alterações de código, API ou schema nesta etapa documental.

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
- O total de horas de um usuário não pode ultrapassar 24 horas em um mesmo dia civil.
- Para períodos que atravessam a meia-noite, cada fração deve ser atribuída ao respectivo dia civil para verificar o limite.
- Dias, relatórios e agrupamentos usam o horário de Brasília.

### Alteração, exclusão e aprovação

- O proprietário do apontamento pode alterar ou excluir seus próprios registros.
- Administradores autorizados podem alterar ou excluir registros de outros usuários dentro da organização e do escopo permitido.
- Usuários comuns não podem alterar ou excluir apontamentos de terceiros.
- Apontamentos não exigem aprovação para serem contabilizados.
- Alterar um apontamento deve reaplicar as mesmas validações de intervalo, sobreposição, limite diário, usuário, tarefa e autorização usadas na criação.

### Precisão e timezone

- A fonte de verdade é o intervalo `StartedAt`/`EndedAt`.
- A duração deve ser calculada a partir desses timestamps, sem arredondamento de negócio.
- Persistência e transporte podem continuar usando `DateTimeOffset`; a apresentação, a separação por dia e os relatórios devem converter os instantes para o horário de Brasília.

## Atores e permissões

- **Usuário autenticado:** inicia e encerra seu cronômetro, cria lançamentos manuais e consulta dados permitidos.
- **Proprietário do apontamento:** altera e exclui seus próprios registros.
- **Administrador autorizado:** altera e exclui registros de usuários pertencentes à organização e ao escopo administrado.
- **Usuário comum:** não altera nem exclui registros de terceiros.
- **Usuário anônimo:** não acessa operações protegidas.

## Comportamento atual comprovado (as-built)

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

## Gaps entre contrato e implementação atual

1. **Alterar e excluir `TimeEntry`:** não foram encontrados commands/endpoints de edição ou exclusão de apontamentos; a autorização do proprietário e do administrador ainda não está implementada nesse fluxo.
2. **Sobreposição:** o lançamento manual não consulta intervalos existentes do usuário e pode sobrepor outro lançamento ou um cronômetro aberto.
3. **Limite de 24 horas por dia:** não existe validação consolidada do total diário, incluindo a divisão de períodos que atravessam a meia-noite.
4. **Timezone uniforme:** a visão semanal usa UTC-03:00, mas `GetMyDailyByTaskQueryHandler` monta o intervalo diário com UTC. Os agrupamentos ainda não seguem uma única política de horário de Brasília.
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
- **Alterado:** início, fim ou observação foram corrigidos por ator autorizado, quando o gap de edição for implementado.
- **Excluído:** registro removido do fluxo de consulta por ator autorizado, quando o gap de exclusão for implementado.
- **Rejeitado:** validação temporal, sobreposição, limite diário, acesso ou referência impede a operação.

Esses estados descrevem o ciclo operacional; não exigem a criação de um enum persistido.

## Persistência

- `TimeEntry` é a fonte de verdade dos apontamentos e mantém timestamps com offset.
- `DurationSeconds` é derivado do intervalo e não representa uma coluna independente.
- `IsManual` diferencia lançamento manual de cronômetro.
- `DayJustification` implementa `IOrganizationOwned` e permanece isolado por organização.
- Esta spec não autoriza migration nem alteração de schema. Caso uma constraint seja necessária para garantir cronômetro único ou outra regra no banco, deve ser submetida ao `G-MIGRATION` antes da implementação.

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

Os endpoints necessários para corrigir e excluir apontamentos são gaps; seus contratos HTTP devem ser definidos na tarefa de implementação, sem alterar as regras funcionais aprovadas nesta spec.

## Interface

- Deve oferecer cronômetro e lançamento manual.
- Deve permitir que o proprietário altere e exclua seus apontamentos e que administradores autorizados façam o mesmo dentro de seu escopo.
- Deve permitir apontamentos em tarefas concluídas.
- Deve comunicar claramente conflitos de sobreposição e estouro do limite diário.
- Deve exibir tempos sem arredondamento para blocos de minutos.
- Datas e agrupamentos diários devem seguir o horário de Brasília.
- Não deve apresentar aprovação de horas como etapa obrigatória.

## Validações e erros

- Rejeitar fim menor ou igual ao início.
- Rejeitar período que sobreponha qualquer apontamento existente do mesmo usuário.
- Rejeitar operação que faça o usuário ultrapassar 24 horas em qualquer dia civil de Brasília afetado pelo intervalo.
- Ao alterar, desconsiderar o próprio registro na busca por conflito e revalidar o intervalo completo.
- Rejeitar usuário ou tarefa inexistente/inacessível.
- Rejeitar alteração ou exclusão de terceiro sem permissão administrativa adequada.
- Não bloquear um lançamento somente porque a tarefa está concluída.
- Erros não devem expor dados de apontamentos de outros usuários.

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
- **Dado** que um intervalo faria o total ultrapassar 24 horas em um dia de Brasília, **quando** houver criação ou alteração, **então** a operação é rejeitada.
- **Dado** um apontamento próprio, **quando** o proprietário o alterar ou excluir, **então** a operação é permitida e os totais são atualizados.
- **Dado** um administrador autorizado, **quando** alterar ou excluir apontamento de usuário do seu escopo, **então** a operação é permitida.
- **Dado** um usuário comum, **quando** tentar alterar ou excluir apontamento de terceiro, **então** a operação é negada sem vazamento de dados.
- **Dado** uma tarefa concluída e acessível, **quando** houver lançamento ou correção de horas, **então** a operação segue as mesmas regras de uma tarefa aberta.
- **Dado** um apontamento válido, **quando** for persistido, **então** ele é contabilizado imediatamente, sem aprovação.
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
- Ausência de endpoints de correção/exclusão impede cumprir a matriz de autorização aprovada.
- Sem teste específico, lançamento em tarefa concluída pode regredir silenciosamente.

## Decisões pendentes

- Política de fechamento mensal ou bloqueio retroativo, caso o produto venha a exigir.
- Regras específicas de conteúdo e unicidade das justificativas diárias.
- Forma técnica de exclusão do apontamento, que não foi definida nesta revisão.

## Rollback

Esta atualização é exclusivamente documental. Não altera produto, dados, endpoints ou schema. A implementação futura dos gaps exigirá planejamento, testes e os gates aplicáveis.

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
