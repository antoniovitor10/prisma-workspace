# SPEC-TEAMS: Equipes da organização

**Status:** approved
**Baseline:** contrato funcional aprovado por PO em 2026-08-24, comparado com a implementação atual.

## Propósito

Definir como pessoas são agrupadas em equipes dentro de uma organização, como essas equipes recebem liderança e acesso operacional e como seu ciclo de ativação afeta membros, tarefas e permissões herdadas.

## Contexto

- `Organization` é o tenant raiz conforme D58.
- Usuário comum pertence a uma única organização; a autoridade de Administrador da plataforma permanece fora dos perfis do tenant.
- Os perfis-base do tenant são Administrador, Gestor, Membro, Visualizador e Externo conforme D55.
- Equipe é um agrupamento interno da organização e não cria um novo tenant.

## Contrato funcional aprovado

### Pertencimento

1. Toda equipe pertence obrigatoriamente a uma única organização.
2. Toda pessoa adicionada precisa ser membro ativo da mesma organização da equipe.
3. Dentro da organização, uma pessoa pode pertencer simultaneamente a várias equipes.
4. A mesma pessoa não pode aparecer duas vezes na mesma equipe.
5. Vínculos de equipes, projetos, quadros, tarefas e pessoas nunca atravessam organizações.

### Cadastro

1. Criar equipe exige:
   - nome;
   - descrição.
2. O nome deve ser único dentro da organização.
3. Nome vazio ou formado somente por espaços é inválido.
4. A descrição deve explicar a finalidade da equipe e não pode ficar vazia.
5. Administrador e Gestor podem criar e editar equipes nos escopos em que estiverem autorizados.
6. Membro, Visualizador e Externo não criam nem editam equipes por padrão.

### Liderança

1. Líder não é obrigatório.
2. Cada equipe possui no máximo um líder.
3. O líder precisa ser membro ativo da própria equipe.
4. Remover o líder da equipe deixa a equipe sem líder; não escolhe outro automaticamente.
5. Ser líder não concede permissão para adicionar ou remover membros.
6. Apenas Administrador e Gestor autorizados administram membros da equipe.

### Membros

1. Administrador e Gestor podem adicionar e remover membros nos escopos autorizados.
2. Adicionar uma pessoa a uma equipe não a remove de nenhuma outra equipe da mesma organização.
3. Remover uma pessoa de uma equipe remove somente os acessos herdados daquele vínculo.
4. Acessos individuais ou herdados de outras equipes permanecem ativos.
5. A resolução de autorização aplica `deny` explícito antes dos acessos herdados, conforme D55.

### Ciclo de vida

1. Equipe possui somente os estados `Ativa` e `Desativada`.
2. Não existe exclusão permanente de equipe.
3. Administrador e Gestor autorizados podem desativar e reativar equipes.
4. Ao desativar:
   - membros e demais vínculos históricos são preservados;
   - acessos herdados exclusivamente daquela equipe deixam de produzir efeito;
   - acessos individuais e provenientes de outras equipes permanecem;
   - a equipe não pode receber novos membros;
   - a equipe não pode receber novas tarefas;
   - a equipe fica indisponível nos seletores operacionais comuns.
5. Ao reativar:
   - os membros preservados voltam a compor a equipe automaticamente;
   - os acessos herdados anteriores voltam a produzir efeito;
   - a equipe volta a aceitar novos membros e novas tarefas.

### Relações operacionais

1. Uma equipe pode ser vinculada a múltiplos projetos da mesma organização.
2. Um projeto pode possuir múltiplas equipes.
3. Usuários também podem manter vínculos individuais com projetos, quadros e tarefas, independentemente das equipes.
4. Vínculos e acessos derivados de equipes seguem os escopos e a precedência definidos em D52 e D55.
5. Uma tarefa pode referenciar uma equipe responsável, mas uma equipe desativada não pode ser selecionada para uma nova atribuição.

### Capacidade e jornada

1. Capacidade semanal da equipe fica oculta por enquanto.
2. Capacidade e jornada individual também ficam ocultas por enquanto.
3. Campos e dados internos legados podem ser preservados para compatibilidade, sem aparecer em telas, formulários, cartões, cálculos ou indicadores funcionais.
4. Esta especificação não exige cálculo de capacidade, disponibilidade ou utilização da equipe.

## Fora do escopo atual

- Exclusão física de equipes.
- Hierarquia de equipes ou subequipes.
- Mais de um líder por equipe.
- Líder com poderes administrativos automáticos.
- Capacidade semanal, jornada individual e cálculos de utilização visíveis.
- Usuário comum pertencendo a mais de uma organização.
- Transferência de equipe entre organizações.

## Estado atual comprovado no código

- `Team` contém `OrganizationId`, `Name`, `LeaderId`, `IsActive`, membros e projetos.
- `TeamMember` usa a chave composta `(TeamId, UserId)`: bloqueia duplicação na mesma equipe e permite que uma pessoa pertença a equipes diferentes.
- O índice `(OrganizationId, Name)` é único.
- A inclusão de membro valida que a pessoa é membro ativo da organização.
- `LeaderId` é anulável, singular e só aceita alguém que já pertença à equipe.
- Remover o líder entre os membros limpa `LeaderId`.
- A API possui operações para listar, criar, atualizar, desativar/reativar, adicionar/remover membros e vincular equipes a projetos.
- `DELETE /api/teams/{id}` apenas desativa a equipe; não remove fisicamente o registro.
- A tela `Teams` permite criar, editar, desativar, reativar, definir líder, administrar membros e vincular projetos.
- O backend usa `PermissionScope.Team` e permissões genéricas para ler, editar, desativar e administrar membros.
- O `WorkItem` possui `TeamId` opcional e pode herdar equipe do quadro no modelo legado.
- O modelo atual persiste capacidade padrão e individual e calcula utilização semanal.

## Gaps entre contrato e implementação

1. **Descrição ausente:** `Team`, DTOs, requests, API e formulário atuais não possuem descrição obrigatória.
2. **Perfis D55 incompletos:** o catálogo e a interface ainda usam papéis legados e não comprovam integralmente a matriz Administrador/Gestor para gestão de equipes.
3. **Ações visíveis sem RBAC explícito:** a tela atual renderiza controles administrativos sem comprovar ocultação por perfil e escopo.
4. **Desativação e acesso herdado:** o estado `IsActive` é alterado, mas não foi comprovado que a autorização suspende somente os acessos herdados daquela equipe e preserva as outras origens.
5. **Reativação de acesso:** não foi comprovado que os acessos herdados preservados voltam automaticamente a produzir efeito.
6. **Atribuição de tarefas:** os fluxos que aceitam `TeamId` validam a existência, mas não comprovam de forma uniforme que a equipe está ativa antes de uma nova atribuição.
7. **Seletores:** precisa ser comprovado que equipes desativadas são omitidas de todos os seletores de nova associação, sem desaparecer do histórico.
8. **Capacidade visível:** a tela atual mostra e permite editar capacidade padrão, capacidade individual, uso semanal e total da equipe, contrariando a decisão de ocultar o recurso.
9. **Testes dedicados insuficientes:** existem testes pontuais de líder, tenant e persistência, mas não uma suíte completa do ciclo de equipes, múltiplo pertencimento, RBAC e herança de acesso.
10. **Homologação manual:** criação, edição, múltiplas equipes, liderança, desativação/reativação e preservação seletiva de acesso ainda precisam ser validadas na interface.

## Regras de domínio

1. `OrganizationId` da equipe é imutável após sua criação.
2. Nome e descrição são obrigatórios e normalizados antes de validar unicidade.
3. A chave lógica de membro permanece `(TeamId, UserId)`.
4. Uma equipe possui zero ou um líder.
5. O líder precisa estar na coleção de membros enquanto ocupar a função.
6. Nenhuma mutação pode vincular pessoa, projeto, quadro ou tarefa de outro tenant.
7. Desativar não apaga equipe, membros, projetos, tarefas nem histórico.
8. A eficácia do acesso por equipe depende de `Team.IsActive`.
9. Remover um vínculo não pode revogar permissões obtidas por outra origem.
10. A equipe inativa não aceita novas associações operacionais.
11. Reativar recupera os vínculos preservados sem duplicá-los.

## Persistência

### Implementado

- `Teams` com organização, nome, líder, estado e dados legados de capacidade.
- `TeamMembers` com PK composta por equipe e usuário.
- `ProjectTeams` para relação N:N entre projetos e equipes.
- `WorkItem.TeamId` opcional.

### Necessário para cumprir o contrato

- Adicionar descrição persistida à equipe.
- Garantir que consultas e resolução de autorização considerem `IsActive` ao aplicar acesso herdado.
- Preservar as origens de acesso de forma que a desativação/removação de uma equipe não elimine grants individuais ou provenientes de outras equipes.
- Manter dados legados de capacidade ocultos ou removê-los futuramente somente após auditoria de consumidores.

Adicionar `Description` ou alterar o modelo de autorização/persistência exige análise e `G-MIGRATION` antes da implementação.

## API esperada

- Listar equipes acessíveis da organização.
- Criar equipe com nome e descrição.
- Editar nome, descrição e líder opcional.
- Desativar e reativar sem exclusão física.
- Adicionar e remover membros conforme RBAC.
- Vincular e desvincular projetos/quadros dentro do mesmo tenant conforme os respectivos contratos.
- Recusar novas associações a equipe desativada.
- Não expor capacidade e jornada nos DTOs funcionais de interface enquanto o recurso estiver oculto.

## Interface esperada

- Lista de equipes ativas, com opção explícita de consultar desativadas.
- Criação exigindo nome e descrição.
- Edição de nome, descrição e líder opcional.
- Administração de membros somente para Administrador e Gestor autorizados.
- Nome funcional das pessoas, usando e-mail apenas como fallback.
- Indicação clara do estado ativa/desativada.
- Confirmação antes da desativação, explicando a suspensão dos acessos herdados.
- Reativação disponível somente a Administrador e Gestor autorizados.
- Nenhuma informação ou controle de capacidade/jornada visível.

## Critérios de aceite

- **Dado** um Administrador ou Gestor autorizado, **quando** criar uma equipe com nome e descrição válidos, **então** ela é criada ativa na organização corrente.
- **Dado** uma pessoa ativa na organização, **quando** adicioná-la a duas equipes distintas, **então** ambos os vínculos coexistem.
- **Dado** uma pessoa já vinculada, **quando** adicioná-la novamente à mesma equipe, **então** a operação é recusada sem duplicação.
- **Dado** uma equipe sem líder, **quando** consultá-la, **então** ela permanece válida.
- **Dado** uma equipe com líder, **quando** removê-lo dos membros, **então** a equipe fica sem líder e nenhum substituto é escolhido automaticamente.
- **Dado** um líder sem perfil de gestão, **quando** tentar adicionar ou remover membros, **então** a operação é recusada.
- **Dado** uma equipe ativa, **quando** desativada, **então** seus membros e vínculos são preservados, mas somente os acessos herdados dela deixam de produzir efeito.
- **Dado** uma pessoa com acesso individual ou por outra equipe, **quando** uma de suas equipes é desativada, **então** essas outras origens continuam válidas.
- **Dado** uma equipe desativada, **quando** tentarem adicionar membro ou atribuir nova tarefa, **então** a operação é recusada.
- **Dado** uma equipe desativada, **quando** reativada, **então** membros e acessos herdados preservados voltam automaticamente sem duplicação.
- **Dado** qualquer perfil, **quando** abrir as telas funcionais, **então** capacidade e jornada da equipe ou dos membros não são exibidas.
- **Dado** uma tentativa de exclusão permanente, **quando** processada, **então** ela é recusada ou convertida em desativação lógica.

## Testes e homologação

### Evidência automatizada necessária

- Domínio: nome/descrição obrigatórios, nome único, líder opcional e singular, líder membro e membro sem duplicidade.
- Persistência: uma pessoa em várias equipes da mesma organização e isolamento entre tenants.
- Autorização: matriz Administrador/Gestor/Membro/Visualizador/Externo e precedência de `deny`.
- Integração: suspensão/restauração de acesso herdado sem afetar grants individuais ou de outras equipes.
- API: equipe desativada não recebe membro, tarefa, projeto ou quadro novo.
- React: ações administrativas conforme perfil, nomes funcionais e ausência total de capacidade/jornada.
- Playwright: criar, editar, adicionar a múltiplas equipes, definir/remover líder, desativar, validar acesso e reativar.

### Homologação manual pendente

1. Criar equipe com nome e descrição.
2. Adicionar a mesma pessoa a duas equipes.
3. Definir e remover líder sem conceder gestão de membros.
4. Desativar e confirmar que novas associações ficam bloqueadas.
5. Confirmar que somente o acesso herdado da equipe desativada é suspenso.
6. Reativar e confirmar a recuperação automática de membros e acessos.
7. Confirmar que capacidade e jornada não aparecem em nenhuma superfície de equipes.
8. Confirmar que não existe exclusão permanente.

## Riscos

- Revogação excessiva pode remover acesso individual ou proveniente de outra equipe.
- Ignorar `IsActive` na autorização pode manter acesso indevido por equipe desativada.
- Dados cruzados de tenant podem aparecer se uma associação aceitar identificador de outra organização.
- Ocultar somente a interface de capacidade sem revisar consumidores pode manter cálculos funcionais inconsistentes.
- Reativação pode duplicar grants se a origem do acesso não for identificável.

## Decisões ainda abertas

- Limite máximo de membros por equipe.
- Se nomes de equipes desativadas podem ser reutilizados por uma nova equipe.
- Política de ordenação e busca da lista de equipes.
- Se o histórico administrativo da equipe terá uma tela própria ou será consultado apenas pela auditoria geral.

## Referências

- `AGENTS.md`
- `DECISIONS.md` — D19, D52, D55 e D58
- `ROADMAP.md`
- `specs/organizations.md`
- `specs/user-access-permissions.md`
- `specs/project-management.md`
- `specs/boards-stages-wip.md`
- `src/Detran.Kanban.Domain/Entities/Team.cs`
- `src/Detran.Kanban.Application/Features/Teams/TeamsFeature.cs`
- `src/Detran.Kanban.Api/Controllers/TeamsController.cs`
- `src/Detran.Kanban.Infrastructure/Persistence/Configurations/TeamConfiguration.cs`
- `src/Detran.Kanban.Infrastructure/Repositories/TeamRepository.cs`
- `src/Detran.Kanban.Web/src/pages/Teams.tsx`

## Rollback

Esta revisão altera somente o contrato documental. Nenhuma entidade, migration, endpoint, tela ou dado foi modificado.

## Rastreabilidade

D19 + D52 + D55 + D58 → `SPEC-TEAMS` → implementação atual + gaps → futura tarefa de aderência → testes automatizados → homologação manual.
