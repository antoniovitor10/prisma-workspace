# SPEC-AUDIT-LEADTIME-HISTORY: Auditoria, lead time e histórico

**Status:** approved
**Baseline:** contrato revisado e aprovado por PO. O estado atual comprovado e as diferenças ainda não implementadas são registrados separadamente abaixo.

## Objective

### Propósito

Definir o histórico auditável das tarefas e a coleta interna dos tempos de permanência por etapa. O histórico deve registrar todas as alterações da tarefa, permanecer separado dos comentários e preservar os registros originais. Lead time e cycle time continuam sendo coletados internamente, mas ficam ocultos na interface por enquanto.

## Approved Contract

### Decisões humanas vigentes

1. O histórico auditável deve mostrar todas as alterações da tarefa.
2. Comentários continuam existindo em área própria, mas não fazem parte do histórico auditável.
3. Os registros do histórico são mantidos permanentemente, sem expiração automática.
4. Um registro original nunca é apagado nem sobrescrito.
5. Administradores podem registrar uma correção adicional vinculada ao registro incorreto, preservando o original.
6. Não haverá exportação do histórico por enquanto.
7. Senhas, tokens, chaves e segredos devem ser mascarados ou omitidos.
8. E-mails e demais dados não secretos só podem ser exibidos conforme as permissões do usuário.
9. O ator deve ser apresentado pelo nome completo; o e-mail é usado apenas como fallback quando o nome não estiver disponível.
10. A ordem padrão do histórico é da alteração mais recente para a mais antiga, com desempate determinístico.
11. Lead time e cycle time não aparecem na interface por enquanto.
12. A captura interna de `StageHistory` e os cálculos já existentes podem continuar ativos para preservar dados futuros.

## Current Verified State

### Persistência e backend

- `WorkItem` é a entidade canônica de tarefa.
- `TaskEvent` registra eventos de tarefa com `ActorId`, `Kind`, `Payload` JSON e `CreatedAt`.
- `Comment` possui persistência e endpoint próprios, separado de `TaskEvent`.
- `StageHistory` registra `EnteredAt`, `LeftAt`, `ActorId`, `ActorName` e `Reason`.
- `MoveWorkItemCommandHandler` encerra o período atual e abre um novo durante a movimentação. Há evidência automatizada em `MoveWorkItemCommandHandlerTests`.
- `AppDbContext` cria `AuditLog` para tipos funcionais auditados durante `SaveChanges` e `SaveChangesAsync`.
- Os snapshots de `AuditLog` mascaram propriedades sensíveis por fragmentos de nome, incluindo `password`, `token`, `hash`, `secret`, `accesskey` e códigos de segurança.
- A consulta administrativa `GET /api/audit` exige a permissão `AdministerOrganization` e respeita o isolamento por organização.
- O endpoint `GET /api/WorkItems/{workItemId}/events` consulta eventos com autorização de leitura da tarefa.
- O endpoint `GET /api/WorkItems/{workItemId}/comments` consulta comentários separadamente.
- O endpoint `GET /api/Boards/{boardId}/lead-time` continua disponível no backend e retorna agregados por etapa do quadro.
- Não foi encontrada rotina de expiração automática de `TaskEvent`, `StageHistory` ou `AuditLog`.

### Interface atual

- O detalhe da tarefa possui abas distintas de `Comentários` e `Histórico`.
- Em modo `history`, `TaskFeed.tsx` carrega apenas eventos; comentários não são misturados nessa visualização.
- A tela ainda possui ação e modal de `Lead Time` no Kanban.
- O repositório e o componente de histórico ordenam os eventos mais antigos primeiro.
- A interface resolve o nome do ator usando o payload e a lista de usuários, mas o contrato do evento não contém um campo próprio e obrigatório para o snapshot do nome.

## Implementation and Validation Gaps

1. **Ocultação das métricas:** a ação e o modal de Lead Time ainda estão visíveis no Kanban; devem ficar ocultos junto com qualquer apresentação de cycle time.
2. **Cobertura integral:** `TaskEvent` é criado para várias operações, e `AuditLog` cobre tipos auditados, mas não há evidência consolidada de que todas as alterações da tarefa apareçam no histórico funcional exibido ao usuário.
3. **Ordenação:** backend e frontend retornam/exibem eventos em ordem crescente; o contrato exige mais recentes primeiro.
4. **Nome do ator:** não há `ActorName` próprio em `TaskEventDto`; parte da identificação depende do payload ou da resolução atual de usuários. É necessário garantir nome completo e e-mail apenas como fallback.
5. **Correção aditiva:** não há fluxo comprovado para administrador registrar correção vinculada ao evento original.
6. **Retenção permanente:** a ausência de expurgo automático é compatível com a decisão, mas ainda faltam política explícita e testes que impeçam exclusão ou sobrescrita funcional dos registros.
7. **Mascaramento ponta a ponta:** `AuditLog` possui redação automática, mas falta comprovar que payloads de todos os `TaskEvent` nunca exponham senhas, tokens, chaves ou segredos.
8. **Homologação manual:** ainda é necessário validar, módulo por módulo, se todas as alterações relevantes aparecem com ator, instante, antes/depois e motivo quando aplicável.

## Scope

### Escopo incluído

- Eventos de criação, edição, movimentação, mudança de status, atribuição, horas, anexos, relacionamentos, conclusão, reabertura e arquivamento de tarefa.
- Histórico de entrada e saída das etapas.
- Identificação do ator e da origem de sistema.
- Valores anterior e novo quando a alteração possuir esses valores.
- Motivo quando informado ou exigido pelo fluxo.
- Correções administrativas aditivas.
- Separação entre histórico automático e comentários.
- Captura interna de dados para tempo por etapa, lead time e cycle time.
- Consulta respeitando organização, projeto e permissões da tarefa.

### Escopo excluído por enquanto

- Exibição de lead time, cycle time ou tempo por etapa na interface.
- Exportação do histórico em CSV, PDF ou outro formato.
- Event sourcing ou reconstrução integral do banco em qualquer instante.
- Alteração ou exclusão silenciosa de registros históricos.
- Comentários dentro da linha do tempo auditável.
- Dashboards consolidados, tratados por `dashboards-reports.md`.
- Logs técnicos de infraestrutura e do Serilog.

## Actors and Permissions

- Usuário com permissão de leitura da tarefa pode consultar o histórico correspondente.
- Usuário com permissão de escrita pode realizar ações que geram eventos, mas não editar o evento gravado.
- Administrador autorizado pode consultar auditoria administrativa e registrar uma correção aditiva.
- O administrador não pode apagar nem sobrescrever o registro original corrigido.
- O sistema registra ações automáticas com origem explícita, sem simular um ator humano.
- E-mails e demais dados pessoais seguem o escopo de autorização do usuário que consulta.

## Functional Requirements

1. Toda alteração da tarefa deve produzir uma entrada auditável acessível no histórico funcional.
2. Cada entrada deve conter tarefa, tipo da alteração, data/hora UTC, ator ou origem de sistema e detalhes compatíveis com o tipo.
3. Alterações de campo devem preservar valores anterior e novo quando tecnicamente aplicável.
4. O histórico deve ser exibido do evento mais recente para o mais antigo.
5. Em empate de instante, deve existir um critério determinístico de ordenação.
6. O ator deve ser exibido pelo nome completo observado ou resolvido; sem nome, usar o e-mail como fallback.
7. Senhas, tokens, chaves e segredos nunca podem aparecer no payload, nos snapshots ou na interface.
8. Comentários são conversas humanas separadas e não geram linhas no histórico auditável apenas por serem publicados.
9. Uma movimentação encerra o período aberto da etapa anterior e inicia um único período na nova etapa.
10. O período encerrado usa `LeftAt - EnteredAt`; o período atual permanece com `LeftAt` nulo.
11. Reentrada em uma etapa cria um novo período e não reabre o registro anterior.
12. Lead time e cycle time continuam ocultos na interface, mesmo com dados internos disponíveis.
13. Uma correção administrativa cria novo evento, identifica o registro corrigido, informa o administrador, o instante e o motivo, sem alterar o original.
14. Nenhum endpoint de exportação do histórico deve ser oferecido nesta versão.
15. Nenhuma rotina funcional deve expirar automaticamente os registros históricos.
16. Dados legados incompletos devem ser sinalizados; o sistema não pode fabricar ator, instante ou valor anterior.

## Invariants

- No máximo um `StageHistory` aberto por tarefa.
- `LeftAt` é nulo em período aberto e maior ou igual a `EnteredAt` em período encerrado.
- Eventos e correções são append-only no fluxo funcional.
- Uma correção não substitui nem remove o registro original.
- Comentários e eventos auditáveis permanecem em contratos e visualizações distintas.
- A soma por etapa preserva múltiplas passagens pela mesma etapa.
- Histórico não possui expiração automática.
- A autorização da consulta deriva do acesso à organização, ao projeto e à tarefa.

## Data Impact

### Conceitos existentes

- `AuditLog`: auditoria transversal com organização, usuário, instante, ação, entidade, snapshots anterior/novo, origem, IP e correlação.
- `TaskEvent`: evento funcional da tarefa com ator, tipo, payload e instante.
- `StageHistory`: período de permanência da tarefa em uma etapa, incluindo snapshot de autoria da transição.
- `Comment`: conversa humana interna, persistida separadamente.
- `WorkItem`: agregado canônico da tarefa.

### Evoluções que exigem gate

- Campo próprio de snapshot do nome do ator em `TaskEvent`, vínculo de correção, índices ou qualquer mudança de schema exigem `G-MIGRATION`.
- Mudança estrutural da imutabilidade ou semântica do histórico exige `G-HISTORY`.
- Mudanças nas regras de transição e captura de `StageHistory` exigem `G-WORKFLOW`.
- Datas persistidas permanecem em UTC.

## Contracts

### API atual

- `GET /api/WorkItems/{workItemId}/events`: eventos automáticos da tarefa.
- `GET /api/WorkItems/{workItemId}/comments`: comentários internos, separados dos eventos.
- `GET /api/Boards/{boardId}/lead-time`: agregado interno existente por etapa do quadro; sua existência no backend não autoriza exibição na interface.
- `GET /api/audit`: auditoria administrativa paginada, restrita à permissão `AdministerOrganization`.

### Interface aprovada

- `Comentários` e `Histórico` permanecem em abas separadas.
- O histórico exibe somente eventos auditáveis, nunca comentários.
- Cada linha apresenta nome do ator, ação, instante e detalhes disponíveis.
- A ordem visual é mais recente primeiro.
- Lead time, cycle time e tempo por etapa ficam ocultos.
- Não há ação de exportação.

## Error Cases

- `400 Bad Request`: filtros, período, paginação ou ordenação inválidos.
- `401 Unauthorized`: autenticação ausente ou inválida.
- `403 Forbidden`: usuário sem acesso à organização, ao projeto ou à tarefa.
- `404 Not Found`: tarefa ou evento não existente ou não visível.
- `409 Conflict`: transição concorrente, inconsistência de período aberto ou correção concorrente.
- Payload sensível ou inválido não deve ser persistido nem exibido sem redação segura.
- Quando alteração e evento integram a mesma transação, falha no histórico deve impedir a confirmação da alteração.

## Acceptance Criteria

- **Given** uma tarefa alterada por usuário autorizado
  **When** o histórico é consultado
  **Then** a alteração aparece com nome completo do ator, instante, antes/depois quando aplicável e sem segredos.
- **Given** vários eventos da mesma tarefa
  **When** a aba Histórico é aberta
  **Then** o evento mais recente aparece primeiro e comentários não aparecem nessa lista.
- **Given** um comentário publicado
  **When** o usuário consulta Comentários e Histórico
  **Then** o comentário aparece somente na área de Comentários.
- **Given** um administrador corrigindo informação histórica
  **When** registra a correção com motivo
  **Then** um novo evento vinculado é criado e o registro original permanece intacto.
- **Given** um evento antigo
  **When** qualquer prazo de retenção é alcançado
  **Then** o evento continua disponível conforme as permissões aplicáveis.
- **Given** dados internos de lead time ou cycle time existentes
  **When** o usuário navega pela interface
  **Then** nenhuma ação, modal, indicador ou relatório dessas métricas é exibido.
- **Given** um usuário sem acesso à tarefa
  **When** solicita eventos ou auditoria
  **Then** nenhum dado é revelado.

## Test Gate Mapping

- Testes de integração para autorização, isolamento por organização e ordenação decrescente.
- Testes de handler para atomicidade entre alteração e evento.
- Testes que comprovem cobertura das alterações da tarefa.
- Testes de redação de senhas, tokens, chaves e segredos em `AuditLog` e `TaskEvent`.
- Testes de imutabilidade, retenção permanente e correção aditiva.
- Testes de interface para separação de comentários, nome do ator, ordem decrescente e ausência de exportação.
- Testes de interface que comprovem a ocultação de lead time e cycle time.
- Homologação manual do histórico com alterações reais de cada módulo da tarefa.

## Risks

- Eventos funcionais não cobrirem todas as alterações registradas pela auditoria transversal.
- Payloads legados sem nome do ator ou sem valores anterior/novo.
- Vazamento de dados sensíveis em payloads construídos manualmente.
- Crescimento permanente das tabelas exigir índices e estratégia operacional de armazenamento sem apagar o histórico funcional.
- Divergência entre `TaskEvent`, `AuditLog` e `StageHistory` para a mesma ação.
- Métricas permanecerem acessíveis por uma superfície esquecida mesmo após serem ocultadas no Kanban.

## Pending Validation

- Homologar manualmente quais alterações já aparecem na aba Histórico.
- Confirmar todas as superfícies onde lead time e cycle time ainda aparecem.
- Verificar o fallback real do ator quando o nome completo não está disponível.
- Validar que payloads manuais de `TaskEvent` respeitam a mesma política de segredos do `AuditLog`.
- Medir volume e desempenho da retenção permanente antes do piloto institucional.

## Rollback

- A ocultação visual das métricas pode ser revertida sem remover `StageHistory` nem seus agregados.
- Nenhum rollback pode apagar eventos históricos já persistidos.
- Mudanças de schema só podem ser revertidas após `G-MIGRATION` e com preservação integral dos dados.

## Traceability

- `AGENTS.md`.
- `DECISIONS.md`: D5, D17, D18, D34, D44, D46 e D47.
- `ROADMAP.md`: Fases 1, 5, 7 e 8.
- `specs/task-history.md`.
- `specs/work-item-management.md`.
- `specs/workflow-status.md`.
- `specs/dashboards-reports.md`.
- Evidências atuais: `AppDbContext.cs`, `TaskFeedRepository.cs`, `TaskFeed.tsx`, `TaskEvent.cs`, `StageHistory.cs`, `TaskFeedController.cs`, `BoardsController.cs`, `MoveWorkItemCommandHandlerTests.cs` e `PlatformCrossCuttingTests.cs`.
- Fluxo: `CAND-AUDIT-LEADTIME-HISTORY -> SPEC-AUDIT-LEADTIME-HISTORY -> task -> tests -> commit`.
