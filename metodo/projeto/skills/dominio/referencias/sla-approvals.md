# SPEC-SLA-001: SLA e aprovações de tarefas

**Status:** approved
**Baseline:** contrato revisado com PO em 2026-08-24, separado do comportamento atualmente implementado.

## Objetivo

Definir o comportamento de SLA e das aprovações de tarefas sem confundir decisão de produto com evidência de implementação.

## Decisões de produto

### SLA

- Todo recurso de SLA fica **oculto da interface por enquanto**.
- Configurações, entidades, cálculos, endpoints e dados de SLA já existentes podem permanecer no código e no banco para compatibilidade.
- Enquanto estiver oculto, SLA não deve aparecer em configurações de projeto, solicitações, tarefas, filtros, dashboards, relatórios, alertas ou navegação comum.
- Ocultar não significa excluir dados nem desativar silenciosamente cálculos existentes. A ativação futura exige nova revisão funcional.

### Aprovações

- Aprovações de tarefas permanecem disponíveis como recurso inspirado no Runrun.it.
- O recurso é configurável por projeto.
- Administrador ou Gestor define a regra do projeto e seus aprovadores.
- Cada projeto escolhe um destes modos:
  1. todas as tarefas exigem aprovação;
  2. somente tarefas selecionadas exigem aprovação;
  3. somente tarefas que alcançarem colunas específicas exigem aprovação.
- Qualquer membro interno elegível pode ser escolhido como aprovador.
- Quando houver vários aprovadores, o projeto escolhe entre:
  - aprovação de todos; ou
  - aprovação de qualquer um.
- Uma solicitação de aprovação não possui prazo nem expiração automática.
- Ao rejeitar, o aprovador deve escolher uma coluna aberta para a qual a tarefa retornará.
- Depois de uma rejeição, uma nova aprovação pode ser solicitada.

## Fora do escopo atual

- Exibir ou expandir SLA.
- Assinatura digital com validade jurídica.
- Delegação ou substituição automática de aprovadores.
- Aprovação sequencial por ordem de pessoas.
- Prazo, expiração ou escalonamento automático de solicitações de aprovação.
- Aprovação por solicitante externo.
- Integrações externas específicas de aprovação.

## Atores e permissões

- **Administrador:** configura o recurso no projeto, define regras e aprovadores e pode consultar o histórico de decisões.
- **Gestor:** possui as mesmas capacidades de configuração de aprovação, respeitando seu escopo autorizado.
- **Membro:** pode ser escolhido como aprovador e decidir solicitações atribuídas a ele.
- **Visualizador:** somente consulta informações às quais já possuir acesso; não configura nem decide.
- **Externo:** não pode ser aprovador neste contrato.
- A simplificação definitiva de perfis e permissões é tratada na especificação própria de acesso. Esta spec apenas depende dos cinco perfis funcionais aprovados: Administrador, Gestor, Membro, Visualizador e Externo.

## Regras funcionais

1. Aprovações só são exigidas quando o projeto habilitar e configurar o recurso.
2. Administrador ou Gestor deve escolher um dos três modos de aplicação definidos nesta spec.
3. No modo por tarefas selecionadas, a exigência deve ser marcada explicitamente na tarefa.
4. No modo por colunas, a configuração deve identificar quais colunas disparam a aprovação.
5. Somente membros internos elegíveis e com acesso ao contexto da tarefa podem ser escolhidos como aprovadores.
6. Havendo vários aprovadores, o resultado final segue a regra `todos` ou `qualquer um` configurada no projeto.
7. Uma decisão deve registrar tarefa, solicitante, aprovador, resultado, instante e observação quando informada.
8. A mesma decisão não pode ser aplicada duas vezes nem produzir efeitos contraditórios.
9. A aprovação permanece pendente até decisão explícita; não expira automaticamente.
10. Uma rejeição exige a escolha de uma coluna aberta válida do quadro atual e move a tarefa para ela de forma atômica.
11. Se a coluna escolhida deixar de existir ou não estiver aberta, a rejeição deve ser recusada sem decidir nem mover parcialmente a tarefa.
12. Após uma rejeição finalizada, o fluxo pode abrir uma nova solicitação de aprovação.
13. Aprovar deve liberar a progressão ou conclusão prevista pela configuração do projeto, sem apagar decisões anteriores.
14. Solicitações e decisões devem integrar o histórico imutável da tarefa.

## Estados e transições

Estados mínimos:

- `Pendente`
- `Aprovada`
- `Rejeitada`

Transições:

- `Pendente → Aprovada`, quando a regra de `todos` ou `qualquer um` for satisfeita.
- `Pendente → Rejeitada`, após decisão válida e escolha de coluna aberta de retorno.
- `Rejeitada → nova solicitação Pendente`, criando um novo ciclo sem sobrescrever o anterior.
- Uma solicitação finalizada não volta a `Pendente`; a repetição cria outra solicitação.

## Comportamento atualmente implementado (as-built)

### SLA

- `ProjectSlaPolicy` persiste ativação, metas de primeira resposta e resolução, janela de atendimento, dias úteis, fuso, feriados, regras, pausa e alertas.
- `SlaController` expõe `GET` e `PUT` em `/api/projects/{projectId}/sla`.
- Há componentes e indicadores de SLA em configurações de projeto, solicitações externas, detalhe da solicitação e relatórios.
- Portanto, o requisito de manter SLA totalmente oculto **ainda não está atendido**.

### Aprovações

- `Approval` registra uma solicitação com um único `ApproverId`, estados `Pendente`, `Aprovada` e `Rejeitada`, observação opcional e datas de criação/decisão.
- `ApprovalsController` permite listar por tarefa, solicitar, decidir e listar pendências do usuário autenticado.
- O backend valida que o aprovador é usuário ativo da organização e possui acesso de leitura à tarefa.
- Existe somente uma aprovação pendente por tarefa.
- Uma decisão só pode ser feita pelo aprovador atribuído e apenas uma vez.
- Solicitação, aprovação e rejeição geram eventos no feed/histórico da tarefa.
- A interface atual permite selecionar um aprovador, solicitar aprovação e visualizar as três decisões mais recentes.
- Aprovações pendentes também aparecem em `Meu trabalho`.
- Não há expiração automática; depois de uma rejeição é possível criar nova solicitação, pois não resta aprovação pendente.

## Gaps entre contrato e implementação

1. SLA ainda aparece em telas, relatórios e solicitações; deve ficar oculto em todas as superfícies comuns.
2. Não existe configuração de aprovação por projeto.
3. Não existem os modos `todas as tarefas`, `tarefas selecionadas` e `colunas específicas`.
4. A definição de regras e aprovadores ainda não é restrita de forma explícita a Administrador ou Gestor.
5. O modelo atual aceita somente um aprovador por solicitação.
6. Não existem regras de múltiplos aprovadores por `todos` ou `qualquer um`.
7. Rejeitar não exige selecionar coluna aberta e não move a tarefa.
8. Não há vínculo entre aprovação e bloqueio/liberação da conclusão conforme a configuração do projeto.
9. A interface mostra somente as três decisões mais recentes no painel da tarefa, sem navegação explícita para o histórico completo.
10. Não há evidência anexada de homologação manual do fluxo completo.

## Persistência esperada

- Configuração de aprovação por projeto, incluindo modo de aplicação e regra para múltiplos aprovadores.
- Associação ordenada ou não ordenada de aprovadores internos elegíveis.
- Identificação das tarefas ou colunas que exigem aprovação, conforme o modo escolhido.
- Ciclos de aprovação preservados individualmente, sem sobrescrever rejeições e aprovações anteriores.
- Decisões individuais quando houver múltiplos aprovadores.
- Coluna aberta de retorno registrada na rejeição.
- Datas em UTC e trilha de auditoria imutável.
- Qualquer alteração de schema exige `G-MIGRATION`; mudança de transições exige `G-WORKFLOW`; mudança da trilha imutável exige `G-HISTORY`.

## Contratos de API esperados

- Consultar e atualizar a configuração de aprovação do projeto.
- Consultar membros internos elegíveis como aprovadores.
- Criar solicitação segundo a configuração do projeto.
- Consultar ciclos, aprovadores e decisões de uma tarefa.
- Aprovar ou rejeitar uma pendência atribuída ao usuário.
- Na rejeição, receber obrigatoriamente o identificador de uma coluna aberta válida.
- Operações inválidas devem responder com erros padronizados e não produzir alteração parcial.

## Critérios de aceite

- **Dado** qualquer usuário na interface comum, **quando** navegar pelo produto, **então** controles, indicadores, filtros e relatórios de SLA não aparecem.
- **Dado** um projeto sem aprovação habilitada, **quando** uma tarefa avançar ou concluir, **então** nenhuma aprovação é exigida.
- **Dado** um projeto configurado, **quando** Administrador ou Gestor salvar o modo e os aprovadores, **então** a regra passa a valer somente naquele projeto.
- **Dado** o modo por tarefas selecionadas, **quando** uma tarefa não marcada avançar, **então** ela não é bloqueada por aprovação.
- **Dado** o modo por colunas, **quando** a tarefa alcançar uma coluna configurada, **então** a aprovação correspondente é exigida.
- **Dado** vários aprovadores com regra `todos`, **quando** apenas parte aprovar, **então** o ciclo continua pendente.
- **Dado** vários aprovadores com regra `qualquer um`, **quando** um deles aprovar, **então** o ciclo é aprovado.
- **Dado** uma rejeição, **quando** o aprovador não escolher uma coluna aberta válida, **então** nada é decidido ou movido.
- **Dado** uma rejeição válida, **quando** o aprovador escolher a coluna aberta, **então** decisão e movimentação são persistidas atomicamente.
- **Dado** um ciclo rejeitado, **quando** uma nova revisão for necessária, **então** outra solicitação pode ser criada preservando o ciclo anterior.
- **Dado** uma aprovação pendente, **quando** o tempo passa sem decisão, **então** ela permanece pendente sem expiração automática.

## Homologação manual pendente

1. Confirmar que SLA não aparece em configurações, solicitações, tarefas, filtros, dashboards, relatórios ou alertas.
2. Configurar cada um dos três modos de aprovação em projetos distintos.
3. Validar seleção de qualquer membro interno elegível.
4. Validar múltiplos aprovadores nos modos `todos` e `qualquer um`.
5. Rejeitar escolhendo uma coluna aberta e confirmar movimentação e histórico.
6. Tentar rejeitar sem coluna ou com coluna fechada/inexistente e confirmar rollback integral.
7. Solicitar nova aprovação após rejeição.
8. Confirmar que a aprovação não expira automaticamente.
9. Confirmar autorizações de Administrador, Gestor, Membro, Visualizador e Externo.

## Testes necessários

- Unitários para agregação das decisões `todos` e `qualquer um`.
- Unitários para elegibilidade de aprovadores e aplicação dos três modos do projeto.
- Integração para rejeição com movimento atômico e rollback em coluna inválida.
- Integração para novo ciclo após rejeição e preservação do histórico.
- API para autorização, idempotência e conflitos de decisão concorrente.
- Frontend para configuração por projeto e estados de solicitação/decisão.
- E2E para fluxo completo de aprovação e para ausência visual de SLA.

## Riscos

- Ocultar somente parte do SLA e continuar expondo indicadores em relatórios ou solicitações.
- Divergência entre a regra configurada no projeto e o bloqueio efetivo da tarefa.
- Decisões concorrentes produzirem mais de um resultado final.
- Rejeição persistir sem conseguir mover a tarefa, ou mover sem registrar a rejeição.
- Conceder capacidade de configuração ou decisão a usuários sem permissão.

## Pendências de decisão

- Se aprovação ou rejeição exigem justificativa obrigatória.
- Se o solicitante pode ser também aprovador da própria tarefa.
- Como alterações na configuração do projeto afetam ciclos já pendentes.
- Qual evento exato solicita aprovação automaticamente nos modos `todas` e `colunas específicas`.

## Referências

- `AGENTS.md`
- `DECISIONS.md`
- `ROADMAP.md`
- `src/Prisma.Workspace.Domain/Entities/Approval.cs`
- `src/Prisma.Workspace.Application/Features/Approvals/Commands/RequestApprovalCommandHandler.cs`
- `src/Prisma.Workspace.Application/Features/Approvals/Commands/DecideApprovalCommandHandler.cs`
- `src/Prisma.Workspace.Api/Controllers/ApprovalsController.cs`
- `src/Prisma.Workspace.Web/src/features/task/TaskApprovalPanel.tsx`
- `src/Prisma.Workspace.Web/src/features/portal/ProjectSlaSettings.tsx`

## Rollback documental

Esta alteração modifica somente a especificação. Nenhum comportamento, dado ou schema foi alterado. Uma implementação futura deve ocultar SLA sem apagar seus dados e introduzir aprovações configuráveis preservando ciclos já registrados.

## Rastreabilidade

SLA e aprovações → `SPEC-SLA-001` → tarefa futura de alinhamento → testes unitários/integração/E2E → homologação manual → commit futuro.
