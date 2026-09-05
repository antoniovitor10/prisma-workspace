# SPEC-NOTIF-001: Notificações internas, e-mail e atualização em tempo real

**Status:** approved

## Objective

Definir o contrato funcional aprovado para notificações da plataforma e registrar, separadamente, o que já
existe no produto, as lacunas de implementação e a homologação necessária. A atualização de quadros por
SignalR permanece documentada como capacidade técnica existente, mas não substitui a entrega das notificações
ao usuário.

## Context

### Contrato aprovado pelo PO em 24/08/2026

- As notificações são entregues dentro do sistema e por e-mail.
- Somente três famílias de evento geram notificação: atribuição de tarefa, menção e prazo.
- E-mails são enviados imediatamente, de forma assíncrona e sem resumo periódico.
- O usuário pode desativar o canal de e-mail; a notificação dentro do sistema continua habilitada.
- Avisos de prazo são gerados 3 dias antes, 1 dia antes e no dia do vencimento.
- Notificações internas são mantidas por 30 dias.
- No sistema, cada nova notificação aparece na central do sino e também em um toast temporário, clicável e
  não bloqueante. O desaparecimento do toast não marca a notificação como lida.

### Estado atual comprovado no código

- `Notification` persiste organização, destinatário, tipo, título, mensagem, link, referências opcionais,
  leitura, entrega de e-mail, tentativas, deduplicação, criação e `RowVersion`.
- `NotificationPreference` permite hoje ligar ou desligar separadamente os canais in-app e e-mail por tipo.
- `NotificationType` contém 13 tipos: atribuição, menção, comentário, resposta pública, prazo próximo,
  atraso, solicitação externa, resposta externa, SLA próximo, SLA vencido, sprint iniciada, sprint encerrada
  e mudança de status.
- A aplicação publica notificações para eventos adicionais aos três aprovados, incluindo comentário,
  respostas do portal, SLA, sprint e mudança de status.
- Atribuições são publicadas por fluxos de responsáveis, criação, gerenciamento e ações em massa.
- Menções são reconhecidas no fluxo de comentários e publicadas como `Mention`.
- O worker de prazo consulta tarefas não concluídas com vencimento até o dia seguinte e diferencia
  `DeadlineNear` de `TaskOverdue`. Ele não cria os três marcos aprovados de 3 dias, 1 dia e vencimento.
- O envio de e-mail já é assíncrono: o worker consulta a fila a cada 20 segundos, processa até 20 registros e
  tenta entregar cada mensagem até cinco vezes. Não existe resumo periódico.
- A central do sino lista apenas notificações in-app do usuário autenticado, consulta a API a cada 30 segundos,
  mostra contador de não lidas, permite marcar todas como lidas e, ao abrir uma notificação, marca-a como lida
  e navega para seu `Link` quando ele existe.
- Não existe toast conectado à chegada de notificações. O toast encontrado no `Topbar` serve para feedback de
  outras ações da interface e não consome a central de notificações.
- Não existe rotina comprovada para remover notificações com mais de 30 dias.
- O SignalR de quadros usa `/hubs/boards`, grupos por quadro e o evento `boardChanged`; o cliente autorizado
  invalida a projeção e a recarrega. Esse canal não entrega atualmente as notificações da central.

### Lacunas entre contrato e produto atual

| Requisito aprovado | Situação atual | Lacuna |
| --- | --- | --- |
| Somente atribuição, menção e prazo | Existem 13 tipos e diversos emissores ativos | Restringir catálogo, preferências e pontos de publicação |
| In-app sempre ativo; e-mail desativável | Usuário pode desligar também o in-app | Impedir a desativação do canal interno para os três tipos |
| Prazo em 3 dias, 1 dia e no vencimento | Worker cobre até o dia seguinte e também atraso | Implementar os três marcos e suas chaves de deduplicação |
| Retenção de 30 dias | Não há expurgo comprovado | Criar limpeza automática e testes de retenção |
| Central do sino e toast | Existe somente a central com polling | Entregar toast temporário, acessível e clicável |
| E-mail imediato | Fila assíncrona roda a cada 20 segundos | Comportamento é compatível, mas falta homologação e cobertura dos workers |

## Scope

### Incluído

- Notificações de atribuição, menção e prazo.
- Central de notificações do usuário autenticado.
- Toast de nova notificação.
- Preferência do canal de e-mail.
- Entrega assíncrona imediata por e-mail, tentativas e deduplicação.
- Retenção automática de notificações internas por 30 dias.
- Atualização em tempo real do quadro via SignalR, preservada como capacidade técnica independente.

## Out of Scope

- Notificações de comentários sem menção, respostas públicas ou externas, SLA, início ou encerramento de
  sprint e mudança de status.
- Resumo diário, semanal ou qualquer agrupamento de e-mails.
- Push notification de navegador ou aplicativo móvel, SMS e mensageria externa.
- Exportação de notificações.
- Alertas derivados da tela “Meu Trabalho”.
- Mensagens transacionais do Portal Externo, que seguem contrato próprio.

## Actors and Permissions

- O usuário autenticado consulta e altera apenas suas próprias notificações e sua preferência de e-mail.
- O destinatário é obtido do usuário autenticado; a API não aceita um `userId` alternativo para consultar ou
  marcar notificações.
- Os publicadores internos só criam notificação se o destinatário for membro ativo da organização.
- A associação ao grupo SignalR de um quadro exige autenticação e acesso válido ao quadro.

## Functional Requirements

1. **Canais obrigatórios:** toda notificação dos três tipos aprovados deve ser criada no canal in-app e, por
   padrão, também no canal de e-mail.
2. **Preferência:** o usuário pode desativar e reativar o e-mail. Essa preferência não remove nem desativa a
   notificação in-app.
3. **Eventos permitidos:** somente atribuição de tarefa, menção explícita e os três marcos de prazo geram
   notificações deste módulo.
4. **Atribuição:** ao receber uma tarefa, o usuário recebe uma notificação com identificação da tarefa e link
   para abri-la. Não se notifica o usuário quando uma operação o mantém como responsável sem mudança efetiva.
5. **Menção:** uma menção explícita gera notificação para cada usuário mencionado, sem duplicar destinatários
   repetidos no mesmo conteúdo.
6. **Prazo:** uma tarefa aberta com data de entrega gera avisos exatamente 3 dias antes, 1 dia antes e no dia
   do vencimento, considerando a data de entrega armazenada.
7. **Deduplicação de prazo:** cada combinação de tarefa, data de entrega, destinatário e marco temporal gera no
   máximo uma notificação, mesmo que o worker seja executado repetidamente.
8. **Alteração de prazo:** se a data de entrega for alterada, os marcos são recalculados com base na nova data;
   notificações já entregues permanecem como histórico até o fim da retenção.
9. **Tarefa concluída:** tarefas concluídas antes de um marco não geram os avisos posteriores daquele prazo.
10. **E-mail imediato:** a publicação coloca o e-mail na fila durante o mesmo fluxo lógico do evento, e o
    worker inicia a entrega no próximo ciclo disponível. Não há espera por resumo ou horário programado.
11. **Falha de e-mail:** uma falha de SMTP não bloqueia a atribuição, menção ou alteração da tarefa. O envio
    segue a política técnica de tentativas e registra o estado final.
12. **Central do sino:** a notificação in-app fica disponível na central, com título, mensagem, tipo, data,
    estado de leitura e link de contexto quando aplicável.
13. **Toast:** quando uma nova notificação chega durante uma sessão ativa, um toast temporário aparece sem
    bloquear a tela. Ele deve ser acessível por teclado e tecnologia assistiva.
14. **Interação do toast:** clicar no toast abre o contexto indicado pela notificação. O simples desaparecimento
    automático do toast não marca o item como lido.
15. **Persistência no sino:** fechar ou ignorar o toast não remove o item da central. A notificação permanece
    não lida até o usuário abri-la ou executar uma ação explícita de marcação.
16. **Retenção:** notificações internas, lidas ou não lidas, permanecem disponíveis por 30 dias contados de
    `CreatedAt`. Após esse período, são removidas automaticamente da central e da persistência operacional.
17. **Isolamento:** nenhuma notificação pode ser consultada, marcada ou entregue a usuário ou organização
    diferente do destinatário registrado.
18. **Realtime do quadro:** o evento `boardChanged` continua transportando somente identificadores e
    metadados mínimos; clientes autorizados recarregam a projeção, sem dados de negócio no payload.

## Invariants

- Toda notificação aprovada possui representação in-app.
- Desativar e-mail nunca desativa o canal in-app.
- Nenhum evento fora de atribuição, menção e prazo cria notificação neste módulo.
- Um marco de prazo não é duplicado para a mesma tarefa, destinatário, data e antecedência.
- O toast é uma apresentação transitória; a central persistente é a fonte da leitura do usuário.
- Notificações não ultrapassam 30 dias na persistência operacional.
- Usuários só acessam suas próprias notificações dentro da organização ativa.

## Acceptance Criteria

- **Given** um usuário recebe uma nova atribuição
  **When** a operação é concluída
  **Then** a central recebe a notificação e o e-mail é enfileirado imediatamente, salvo se o usuário tiver
  desativado e-mail.
- **Given** um comentário contém uma menção explícita
  **When** o comentário é salvo
  **Then** cada usuário mencionado recebe uma notificação, sem gerar notificação de comentário para os demais.
- **Given** uma tarefa aberta vence em três dias, um dia ou hoje
  **When** o worker de prazo processa o respectivo marco
  **Then** os destinatários recebem uma única notificação daquele marco.
- **Given** uma tarefa já está concluída
  **When** chega um marco futuro de seu prazo
  **Then** nenhuma nova notificação é criada.
- **Given** o usuário desativou e-mails
  **When** ocorre um evento permitido
  **Then** a notificação aparece no sistema e nenhum e-mail é enfileirado.
- **Given** uma notificação chega com o sistema aberto
  **When** o frontend a recebe
  **Then** exibe toast não bloqueante e mantém o item não lido na central.
- **Given** o usuário clica no toast ou no item da central
  **When** existe um link de contexto
  **Then** a SPA abre o destino correspondente e atualiza a leitura conforme a interação realizada.
- **Given** uma notificação completa 30 dias
  **When** a rotina de retenção é executada
  **Then** ela deixa de ser retornada pela API e é removida da persistência operacional.
- **Given** um cliente tenta entrar no grupo SignalR de quadro sem acesso
  **When** chama `JoinBoard`
  **Then** recebe erro e não é associado ao grupo.

## Data Impact

- As tabelas `Notifications` e `NotificationPreferences` já atendem à persistência básica, entrega e
  preferência de e-mail.
- A retenção pode usar `Notifications.CreatedAt`; não há requisito funcional de nova coluna.
- A estratégia técnica deve avaliar índices e exclusão em lotes para evitar bloqueios durante o expurgo.
- Se a implementação exigir alteração de schema ou migration, deve parar no Human Gate `G-MIGRATION`.

## Authorization Impact

- Endpoints continuam sob autenticação.
- Consulta e mutação são sempre limitadas ao destinatário autenticado e à organização ativa.
- Preferência de e-mail é pessoal e não pode ser alterada por outro usuário neste contrato.
- O `BoardHub` mantém validação de associação à organização e ao quadro antes de aceitar `JoinBoard`.

## Contracts

### API atual preservada

- `GET /api/notifications?unreadOnly&page&pageSize` retorna a página e a contagem de não lidas.
- `PUT /api/notifications/{id}/read` marca um item do próprio usuário como lido.
- `PUT /api/notifications/read-all` marca os itens do próprio usuário como lidos.
- `GET /api/notifications/preferences` retorna as preferências do usuário.
- `PUT /api/notifications/preferences/{type}` atualiza a preferência aplicável.

### Ajustes necessários

- O contrato de preferências deve impedir que o canal in-app seja desativado para os três eventos aprovados;
  o cliente deve apresentar somente a preferência de e-mail aplicável.
- A entrega do toast exige um mecanismo de chegada durante a sessão ativa. A escolha entre SignalR dedicado,
  extensão segura de conexão existente ou consulta incremental é técnica, desde que cumpra os critérios sem
  expor dados entre organizações.
- O hub `/hubs/boards` e seu evento `boardChanged` permanecem inalterados até eventual decisão técnica
  específica para o canal de notificações.

## Dependencies

- `AGENTS.md`.
- `DECISIONS.md` (`D24`, `D28`, `D32`).
- `Notification`, `NotificationPreference`, `NotificationCatalog` e `NotificationRepository`.
- `NotificationDeliveryWorker` e `NotificationReminderWorker`.
- `NotificationsController` e `NotificationCenter.tsx`.
- A regra desta spec restringe as preferências genéricas previstas em `D32`: para os três tipos aprovados,
  o canal in-app é obrigatório e somente o e-mail é desativável. A harmonização do texto de `D32` deve ser
  registrada antes da implementação se for considerada mudança arquitetural.

## Error Cases

- Falha de entrega de e-mail não desfaz a operação que originou a notificação.
- SMTP ausente ou destinatário sem e-mail registra entrega ignorada/falha sem ocultar a notificação in-app.
- Link de contexto inválido mantém a notificação consultável e apresenta erro de navegação sem marcá-la como
  entregue com sucesso ao contexto.
- Preferência tentando desativar in-app para um tipo obrigatório deve ser rejeitada ou normalizada para ativo.
- Tentativas duplicadas do mesmo marco de prazo não criam registros adicionais.
- Falha parcial no expurgo deve permitir repetição idempotente sem remover notificações dentro dos 30 dias.

## Pending Decisions

- Nenhuma decisão funcional pendente nesta rodada.
- A tecnologia de entrega do toast e a estratégia de expurgo são decisões de implementação, não mudanças do
  contrato funcional.

## Test Gate Mapping

### Cobertura existente

- `PlatformCrossCuttingTests.Notification_publisher_respects_defaults_preferences_and_deduplication`
  comprova publicação básica, preferência e deduplicação usando EF Core InMemory.
- Não existe evidência automatizada suficiente para afirmar aderência ao contrato aprovado.

### Testes necessários

- Publicação exclusiva de atribuição, menção e prazo; ausência de notificações para os demais eventos.
- Canal in-app obrigatório e e-mail desativável por usuário.
- Marcos de prazo de 3 dias, 1 dia e vencimento, incluindo deduplicação e tarefa concluída.
- Entrega imediata assíncrona, tentativas, falha e ausência de SMTP.
- Expurgo de lidas e não lidas após 30 dias, preservando registros mais novos.
- Central, contador, leitura, navegação e isolamento por usuário/organização.
- Toast temporário, clique, teclado, tecnologia assistiva e permanência do item não lido na central.
- Workers e deduplicação testados contra SQL Server dedicado quando o comportamento depender de índice real.
- Autorização e isolamento do `BoardHub` e formato mínimo de `boardChanged`.

## Manual Validation

### Homologação do módulo

- [ ] Atribuir uma tarefa e confirmar central + toast + e-mail imediato.
- [ ] Mencionar um usuário e confirmar central + toast + e-mail imediato.
- [ ] Salvar comentário sem menção e confirmar que nenhuma notificação é criada.
- [ ] Validar os marcos de 3 dias, 1 dia e vencimento sem duplicação.
- [ ] Desativar e-mail e confirmar que a central e o toast continuam funcionando.
- [ ] Ignorar o toast e confirmar que a notificação permanece não lida no sino.
- [ ] Clicar no toast e confirmar abertura do contexto correto.
- [ ] Confirmar que eventos de status, sprint, portal e SLA não entram na central deste módulo.
- [ ] Confirmar retenção por 30 dias em ambiente de teste controlado.

## Risks

- Eventos extras hoje publicados podem continuar enviando e-mails até que todos os emissores sejam removidos
  ou isolados.
- Polling de 30 segundos não garante toast oportuno e pode mostrar a mesma chegada mais de uma vez se o cliente
  não controlar o último item apresentado.
- Expurgo sem lote e índice adequados pode bloquear a tabela quando houver grande volume.
- O worker de e-mail não possui cobertura isolada comprovada; regressões de tentativa podem ser silenciosas.
- O índice filtrado de deduplicação não é validado pelo provider InMemory usado no teste atual.

## Rollback

- Esta revisão altera somente documentação e não exige rollback de produto.
- A implementação futura deve permitir desabilitar separadamente o mecanismo de toast e a rotina de expurgo,
  sem reativar eventos fora do contrato.

## Traceability

- `CAND-NOTIF-001 -> SPEC-NOTIF-001 -> implementação futura -> testes -> homologação -> commit`.
- Evidências atuais:
  - `src/Prisma.Workspace.Domain/Entities/Notification.cs`.
  - `src/Prisma.Workspace.Domain/Enums/NotificationType.cs`.
  - `src/Prisma.Workspace.Application/Features/Notifications/NotificationsFeature.cs`.
  - `src/Prisma.Workspace.Infrastructure/Repositories/PlatformRepository.cs`.
  - `src/Prisma.Workspace.Infrastructure/Services/NotificationWorkers.cs`.
  - `src/Prisma.Workspace.Api/Controllers/NotificationsController.cs`.
  - `src/Prisma.Workspace.Api/Realtime/BoardHub.cs`.
  - `src/Prisma.Workspace.Web/src/features/notifications/NotificationCenter.tsx`.
  - `tests/Prisma.Workspace.Tests/PlatformCrossCuttingTests.cs`.
