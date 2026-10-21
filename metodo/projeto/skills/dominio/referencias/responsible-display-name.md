# SPEC-RESPONSIBLE-DISPLAY-NAME: Nome Completo nas Identidades da Interface

**Status:** approved
**Revisão humana:** contrato alinhado à decisão explícita de PO registrada em D63.
**Leitura:** esta spec separa o contrato aprovado, o estado comprovado no código e os gaps pendentes.

## Objective

Usar o nome completo como identificação humana canônica em toda a interface e em novos registros históricos, ocultando o e-mail sempre que o nome estiver disponível e preservando compatibilidade temporária com dados legados sem nome.

## Context

O produto usa `IdentityUser` para autenticação e `OrganizationMember.DisplayName` para a identificação humana no tenant. Dados legados ou importados sem nome são dados de teste e serão removidos futuramente. Nesta etapa eles não serão bloqueados, migrados nem preenchidos artificialmente.

## Contrato aprovado por PO

1. Todo novo cadastro de usuário exige nome completo com, no mínimo, nome e sobrenome.
2. O nome completo é a identificação principal em toda a interface: responsável, participantes, histórico, auditoria, cards, listas, seletores, equipes, relatórios, comentários de sistema e demais superfícies que identifiquem uma pessoa.
3. Quando o nome existir, o e-mail não pode aparecer no mesmo rótulo nem como identificação secundária daquela pessoa.
4. O e-mail é fallback temporário somente para registro legado ou importado sem nome.
5. Se nome e e-mail estiverem ausentes, deve ser usado um rótulo neutro baseado em identificador estável, sem inventar autoria.
6. Snapshots históricos preservam o valor observado no momento do evento; não há reescrita retroativa.
7. Registros legados sem nome continuam utilizáveis e não exigem migration ou bloqueio nesta revisão.

## Scope

- Capturar e validar nome completo no cadastro de novos usuários.
- Manter o nome completo associado ao membro da organização ativa como fonte funcional no tenant.
- Resolver a identificação humana por um serviço canônico, respeitando isolamento organizacional.
- Aplicar a mesma regra de exibição em todas as superfícies funcionais e administrativas.
- Gravar o nome resolvido como `ActorName` ou snapshot equivalente em novos eventos, históricos e auditorias.
- Manter o e-mail em campos próprios apenas onde ele é o dado operado, como login, convite, recuperação de conta e edição explícita do endereço de e-mail.

## Out of Scope

- Inferir nome a partir do prefixo do e-mail.
- Preencher ou migrar automaticamente registros legados sem nome.
- Reescrever snapshots históricos existentes.
- Remover e-mail dos contratos de autenticação, convite, recuperação ou contato quando o próprio e-mail for o dado da operação.
- Remover coluna ou compatibilidade existente nesta revisão documental.

## Functional Requirements

1. O cadastro novo deve receber `fullName`, aparar espaços externos e normalizar espaços repetidos.
2. `fullName` deve conter entre 2 e 200 caracteres e pelo menos duas partes não vazias separadas por espaço, representando nome e sobrenome.
3. O cadastro deve ser recusado quando o nome completo estiver ausente, contiver apenas uma parte ou exceder o limite.
4. Ao criar ou ativar a associação organizacional do novo usuário, o nome completo capturado deve ficar disponível como `OrganizationMember.DisplayName` ou contrato funcional equivalente, sem perda entre cadastro, convite e associação.
5. Para cada usuário dentro do tenant, o resolvedor canônico deve seguir esta ordem:
   1. nome completo válido do membro da organização;
   2. e-mail, somente para registro legado/importado sem nome;
   3. `Usuário {shortId}` quando nome e e-mail estiverem ausentes.
6. `UserName` não constitui nome completo e não deve ser exibido como identidade humana, mesmo quando não tiver formato de e-mail.
7. Quando o nome completo existir, DTOs e componentes não podem preferir, concatenar ou mostrar o e-mail como identificação complementar.
8. Alterações futuras no nome refletem consultas e novos eventos; snapshots históricos anteriores permanecem imutáveis.
9. Um nome completo de registro novo não pode ser limpo para `null` ou vazio. Registros legados podem continuar sem nome até sua remoção futura.
10. A ausência de nome em legado não impede leitura, edição, atribuição, movimentação ou auditoria.

## Invariants

- A resolução de nome respeita a organização ativa e nunca consulta associação de outro tenant.
- Nome completo e e-mail são campos semanticamente distintos.
- O e-mail só substitui visualmente o nome quando o registro não possui nome.
- IDs e relacionamentos (`ResponsibleId`, `WorkItemAssignee.UserId` e equivalentes) permanecem estáveis.
- Nenhum processo fabrica nome a partir do e-mail.
- Histórico já persistido não é recalculado após mudança de nome.

## Contracts

- `POST /api/auth/register` passa a receber `fullName`, `email` e `password`, conforme `SPEC-AUTH-001`.
- DTOs de membro e de usuário atribuível expõem `displayName` separadamente de `email`.
- DTOs de tarefas, participantes, equipes, auditoria e histórico usam o nome resolvido no campo de identificação humana.
- O endpoint administrativo de atualização de membro valida o mesmo formato e não permite apagar o nome de um registro novo.
- A forma técnica de preservar o nome entre cadastro e associação deve ser definida na implementação sem criar uma segunda fonte funcional divergente.

## As-built comprovado no código

- `OrganizationMember.DisplayName` existe como `nvarchar(200) NULL` e aceita remoção por `UpdateDisplayName(null)`.
- A validação atual aceita qualquer texto entre 2 e 200 caracteres e não exige nome e sobrenome.
- `RegisterRequest` recebe somente e-mail e senha; a tela de cadastro também não coleta nome.
- `UserDirectory.GetDisplayNamesAsync` delega a `UserDisplayName.Resolve`.
- O resolvedor frontend prioriza `displayName`, depois `userName` sem formato de e-mail e, por fim, `Usuário {shortId}`; ele não aplica o fallback de e-mail legado aprovado.
- Parte das superfícies de tarefa já usa `ResponsibleName`/`DisplayName`, mas existem pontos que ainda leem e-mail diretamente.
- Novos snapshots de alguns controllers ainda usam claim de e-mail como `ActorName`.

## Gaps entre contrato e implementação

| Tema | Contrato aprovado | Estado atual | Situação |
|---|---|---|---|
| Cadastro | nome e sobrenome obrigatórios | `RegisterRequest` e tela recebem somente e-mail e senha | gap de API, UI e validação |
| Persistência inicial | nome não pode se perder até a associação organizacional | não existe contrato no cadastro para transportar o nome | gap de aplicação/persistência |
| Validação | duas partes, 2–200 caracteres, nome não removível em cadastro novo | apenas 2–200 e campo nullable/limpável | gap de domínio/API |
| Fallback legado | e-mail quando o nome não existir | `UserName` não e-mail ou rótulo neutro | gap de resolvedor/backend/frontend |
| Ocultação do e-mail | nome existente oculta e-mail em toda UI | equipes, seletores e áreas administrativas ainda exibem e-mail em alguns pontos | gap de UI/DTO |
| Histórico novo | snapshot usa nome resolvido | alguns controllers usam claim de e-mail | gap de API/histórico |
| Cobertura global | mesma regra em todas as superfícies | adoção parcial, sem inventário automatizado completo | gap de integração/E2E |

## Acceptance Criteria

- **Given** um novo cadastro, **When** o usuário informa apenas um nome, **Then** a operação é recusada junto ao campo de nome completo.
- **Given** `João Silva` e um e-mail cadastrados, **When** João aparece em card, detalhe, participante, equipe, lista, histórico ou auditoria, **Then** aparece `João Silva` e o e-mail não é exibido como sua identidade.
- **Given** um registro legado sem nome e com e-mail, **When** ele aparece na interface, **Then** o e-mail é mostrado como fallback e a operação continua disponível.
- **Given** um legado sem nome e sem e-mail, **When** a autoria é exibida, **Then** aparece `Usuário {shortId}` sem autoria inventada.
- **Given** uma mudança posterior de nome, **When** novos dados são consultados ou novos eventos são gravados, **Then** usam o novo nome e eventos antigos preservam seu snapshot.
- **Given** membros em tenants distintos, **When** uma identidade é resolvida, **Then** somente a associação da organização ativa fornece o nome.

## Test Gate Mapping

- Domínio/validação: trim, espaços repetidos, nome e sobrenome, limites e impossibilidade de limpar nome novo.
- API: cadastro exige `fullName`, persiste/transporta o valor e mantém isolamento organizacional.
- Resolvedor: nome, fallback de e-mail legado, fallback neutro e ausência de `UserName` como identidade.
- Integração: novos `ActorName` usam nome resolvido; snapshots antigos não mudam.
- React: cards, detalhes, participantes, histórico, auditoria, equipes, listas, seletores e relatórios ocultam e-mail quando há nome.
- E2E: cadastrar usuário com nome completo, associá-lo e validar a mesma identidade nas principais superfícies.

## Homologação manual pendente

1. Cadastrar usuário exigindo nome e sobrenome.
2. Confirmar que responsável, participante, card, lista, equipe, histórico e auditoria mostram apenas o nome quando disponível.
3. Confirmar fallback de e-mail em um registro legado sem nome.
4. Alterar o nome e verificar consultas futuras sem reescrever eventos anteriores.

## Data Impact

Esta revisão não aplica migration nem backfill. A coluna nullable existente é preservada para compatibilidade com legados. A implementação deverá definir onde o nome fica retido antes da criação da associação organizacional; se isso exigir schema, deve abrir `G-MIGRATION` antes da alteração.

## Authorization Impact

- A permissão para alterar nomes de membros continua vinculada à administração de membros conforme `SPEC-USER-ACCESS-PERMISSIONS`.
- A resolução continua limitada ao tenant ativo.

## Risks

- Criar duas fontes de nome divergentes entre Identity e membership: mitigar definindo uma origem funcional e sincronização explícita na implementação.
- E-mail continuar vazando por componentes que montam rótulos localmente: centralizar a resolução e cobrir as superfícies por teste.
- Legado sem nome persistir por mais tempo que o previsto: manter fallback sem fabricar dados; a remoção futura será tarefa separada.

## Rollback

Não aplicável à revisão documental. Uma implementação futura deve preservar os nomes já cadastrados e a compatibilidade de leitura dos registros legados.

## Traceability

D63 → SPEC-RESPONSIBLE-DISPLAY-NAME (`approved`) → gaps de cadastro/resolução/UI/histórico → testes .NET/React/E2E → homologação manual.
