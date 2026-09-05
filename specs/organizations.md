# SPEC-ORGANIZATIONS: Organizações e isolamento multi-tenant

**Status:** approved
**Baseline:** target — decisões funcionais aprovadas em 2026-08-24; o código atual possui a base multi-tenant, mas ainda diverge na administração de ciclo de vida.

## Objective

Definir `Organization` como tenant raiz da aplicação, com dados, configurações, permissões e operação estritamente isolados. Usuário comum pertence a uma única organização; somente Administrador da plataforma pode alternar tenants pelo seletor superior.

## Conceito

A organização equivale à conta/empresa raiz, não a um agrupador de projetos. Dentro dela existem usuários, equipes, projetos, quadros, tarefas, sprints, relatórios e configurações. Um recurso funcional nunca pertence simultaneamente a duas organizações.

Esse conceito segue a referência oficial do Runrun.it, em que a `app_key` identifica a conta inteira, o recurso Enterprise representa a empresa do usuário e a conta é operada dentro dessa enterprise. O usuário comum não alterna enterprises; a navegação entre tenants é uma ferramenta exclusiva do control plane. Referências: [Runrun.it API — autenticação e Enterprises](https://secure.runrun.it/api/documentation) e [Runrun.it — regras básicas de uso](https://blog.runrun.it/rr-hacks-5-regras-basicas-para-usar-o-runrun/).

## Scope

- Organização como tenant raiz de todos os recursos internos.
- Associação do usuário comum com exatamente uma organização.
- Seleção da organização no topo visível somente para Administrador da plataforma.
- Troca administrativa imediata de contexto sem recarregar manualmente o navegador.
- Isolamento estrito de leitura, escrita, busca, cache, eventos, relatórios, anexos e tempo real.
- Criação, edição, arquivamento e restauração de organizações exclusivamente pelo Administrador da plataforma.
- Organização arquivada em modo somente leitura, acessível exclusivamente a Administradores da plataforma.
- Preservação integral dos dados e histórico ao arquivar.

## Out of Scope

- Usar organização como agrupador de projetos ou quadro.
- Compartilhar equipes, projetos, quadros, tarefas ou configurações entre organizações.
- Permitir que o mesmo login ou e-mail de usuário comum seja associado a mais de uma organização.
- Permitir que Administrador ou Gestor da organização criem, editem, arquivem ou restaurem o próprio tenant.
- Exclusão física ou lixeira de organização.
- Definir novamente perfis e permissões internos, tratados por `SPEC-USER-ACCESS-PERMISSIONS`.
- Definir novamente autenticação e tokens, tratados por `SPEC-AUTH-001`.

## Papéis de administração

### Administrador da plataforma

É uma autoridade de control plane, externa aos cinco perfis-base da organização definidos na D55. Somente esse papel pode:

- criar uma organização;
- editar seus dados e preferências;
- arquivar uma organização;
- consultar uma organização arquivada em modo somente leitura;
- restaurar uma organização arquivada.

Essa autoridade precisa ser validada no servidor e não pode ser obtida por convite, perfil personalizado, `PermissionGrant` do tenant ou pelo perfil-base Administrador da organização.

### Administrador da organização

Administra recursos e pessoas dentro de uma organização ativa conforme `SPEC-USER-ACCESS-PERMISSIONS`, mas não controla a existência ou os dados cadastrais do tenant. A proteção do último Administrador ativo continua válida para que uma organização ativa não fique sem administração funcional.

## Functional Requirements

1. Um usuário comum possui no máximo uma associação de organização; convite ou cadastro com login/e-mail já associado a outro tenant deve ser rejeitado sem criar vínculo parcial.
2. Usuário comum não visualiza seletor de organizações; sua única organização ativa é resolvida automaticamente.
3. Somente Administrador da plataforma visualiza o seletor superior e pode alternar entre organizações ativas.
4. A troca administrativa de organização atualiza imediatamente o tenant ativo, sem `page.reload()`.
5. Toda requisição autenticada de negócio envia `X-Organization-Id` e o servidor valida organização ativa e associação única ativa antes do caso de uso; no control plane, a seleção do Platform Admin é validada por sua autoridade própria.
6. Respostas pendentes, caches, estados locais, pesquisas, notificações e conexões de tempo real do tenant anterior não podem reaparecer depois da troca administrativa.
7. Rotas e recursos do tenant anterior são fechados; o Platform Admin navega para a raiz segura da organização selecionada e carrega o último quadro acessível conforme D52, ou seu estado vazio.
8. Entidades-raiz persistem `OrganizationId`; entidades-filhas derivam o tenant pelo agregado e não aceitam vínculo cruzado.
9. Filtros globais de leitura e validações de escrita aplicam default deny quando o tenant estiver ausente, inválido ou diferente do recurso.
10. Busca, relatórios, exportações, anexos, auditoria, filas, SignalR e qualquer processamento assíncrono preservam o `OrganizationId` e nunca agregam tenants.
11. Somente Administrador da plataforma cria organizações. Usuários comuns sem associação recebem orientação para solicitar acesso, não um formulário de criação.
12. Somente Administrador da plataforma edita ou arquiva uma organização.
13. Arquivar desativa o tenant para operação normal sem apagar membros, projetos, quadros, tarefas, horas, anexos, configurações ou histórico.
14. Organização arquivada desaparece das APIs normais dos membros e do contexto comum, inclusive para o Administrador da organização; permanece no seletor administrativo do Platform Admin com indicação de arquivada.
15. Somente Administrador da plataforma acessa dados de organização arquivada, sempre em modo somente leitura.
16. Somente Administrador da plataforma restaura a organização; a restauração reativa o tenant preservando seus dados e vínculos.
17. Não existe exclusão física nem purga automática de organização.
18. A criação precisa estabelecer atomicamente uma organização válida e ao menos um Administrador da organização ativo; o método de escolha desse primeiro administrador pertence ao fluxo administrativo da plataforma.

## Invariants

- `Organization` é o limite máximo do tenant funcional.
- Um recurso funcional possui exatamente uma organização proprietária.
- Usuário comum possui no máximo uma associação de organização, inclusive considerando e-mail/login normalizado.
- Administrador da plataforma alterna tenants por autoridade de control plane, não por múltiplas memberships comuns.
- Administrador da plataforma não é um perfil-base de tenant e não pode ser concedido dentro da organização.
- Organização ativa mantém ao menos um Administrador da organização ativo.
- Organização arquivada é imutável para operação funcional e visível somente no control plane da plataforma.
- Arquivamento e restauração são lógicos, auditáveis e não removem histórico.
- Ausência de contexto nunca libera consulta sem filtro.

## Switching and State Isolation

- O usuário comum vê apenas o nome/contexto de sua organização, sem seletor.
- O seletor superior de organizações é exibido exclusivamente ao Administrador da plataforma.
- O identificador selecionado é persistido somente como preferência do usuário; ele nunca substitui a validação server-side.
- Query keys, caches, storage local e preferências que contenham dados funcionais devem ser particionados por `OrganizationId` ou descartados na troca.
- Requisições em andamento são canceladas quando possível; respostas tardias são ignoradas por identidade do tenant.
- O cliente não reutiliza seleção de projeto, quadro, tarefa, sprint, filtro salvo ou modal pertencente ao tenant anterior.
- Eventos SignalR e notificações são assinados e consumidos somente nos grupos da organização ativa.

## Authorization and Contracts

- Para usuário comum, o contrato de organização retorna no máximo seu único tenant ativo e não expõe catálogo de tenants.
- Para Administrador da plataforma, o contrato de control plane pode listar organizações ativas e arquivadas para seleção e administração.
- Endpoints de control plane para criar, editar, arquivar, consultar arquivada e restaurar exigem Administrador da plataforma.
- `X-Organization-Id` continua obrigatório nas APIs autenticadas sujeitas ao middleware de tenant.
- A API responde `403` quando a identidade é conhecida mas não possui autoridade; pode usar `404` para recurso de outro tenant quando a não enumeração for necessária.
- O perfil-base Administrador e a permissão `AdministerOrganization` da D55 governam a operação interna autorizada, não o ciclo de vida do tenant.
- Toda ação de control plane sobre organização é auditada com ator, instante, ação e valores anterior/novo, sem registrar segredos.

## Archive Lifecycle

`Ativa → Arquivada → Ativa`

- **Ativa:** disponível aos membros ativos e plenamente operacional conforme suas permissões.
- **Arquivada:** removida da operação e de qualquer contexto comum; permanece identificável no control plane somente para Administrador da plataforma consultar em modo leitura ou restaurar.
- Não existe transição para exclusão permanente.
- Escritas concorrentes iniciadas antes do arquivamento devem falhar ou ser revertidas; o tenant não pode ficar parcialmente arquivado.

## Acceptance Criteria

- **Given** um usuário comum já associado a uma organização **When** convite ou cadastro tenta vinculá-lo a outra **Then** a operação é rejeitada sem vínculo parcial.
- **Given** um usuário comum autenticado **When** entra na aplicação **Then** sua organização única é carregada e nenhum seletor de tenants é exibido.
- **Given** um Administrador da plataforma **When** usa o seletor superior **Then** alterna imediatamente de tenant e vê apenas os dados da organização escolhida.
- **Given** uma resposta do tenant anterior ainda pendente **When** o Platform Admin troca a organização **Then** essa resposta não altera a nova tela.
- **Given** um identificador de recurso de outro tenant **When** uma API é consultada ou alterada **Then** nenhum dado é retornado ou modificado.
- **Given** um usuário autenticado sem organizações **When** entra na aplicação **Then** não recebe permissão para criar tenant e vê orientação para solicitar acesso.
- **Given** um Administrador da organização que não é Administrador da plataforma **When** tenta criar, editar, arquivar ou restaurar uma organização **Then** a API nega a ação.
- **Given** um Administrador da plataforma **When** cria uma organização válida **Then** o tenant e seu primeiro Administrador da organização são persistidos atomicamente.
- **Given** uma organização arquivada **When** um membro comum ou Administrador da organização consulta a lista ou envia `X-Organization-Id` **Then** ela permanece indisponível.
- **Given** uma organização arquivada **When** o Administrador da plataforma a consulta **Then** os dados são somente leitura e nenhuma alteração funcional é aceita.
- **Given** uma organização arquivada **When** o Administrador da plataforma a restaura **Then** seus dados e vínculos reaparecem sem perda de histórico.

## Current Gaps

1. Não existe papel ou claim de Administrador da plataforma no domínio, Identity, autorização ou UI.
2. `POST /api/organizations` exige apenas autenticação; qualquer usuário autenticado pode criar um tenant.
3. `OrganizationOnboarding` oferece `Crie sua organização` automaticamente para qualquer usuário sem associação.
4. A criação atual torna o próprio solicitante Administrador da organização sem um fluxo de control plane para escolher ou confirmar o primeiro administrador.
5. `PUT /api/organizations/current` usa `AdministerOrganization` do tenant, permitindo edição por autoridade interna em vez de reservar a ação ao Administrador da plataforma.
6. Não existem comandos/endpoints para arquivar ou restaurar organizações.
7. `Organization` possui `IsActive`, mas não possui regras de domínio de arquivamento/restauração, auditoria específica nem bloqueio transacional das escritas em andamento.
8. O repositório comum oculta organizações inativas, porém não existe consulta read-only exclusiva do control plane.
9. O modelo atual permite várias linhas `OrganizationMember` para o mesmo usuário em tenants diferentes e não impõe unicidade global de login/e-mail por organização do usuário comum.
10. Convites podem adicionar um usuário já associado a outra organização; falta rejeição atômica da segunda membership.
11. O seletor superior é exibido para qualquer usuário e lista todas as memberships retornadas, quando deveria existir somente para Platform Admin.
12. O cancelamento de queries existe, mas a troca atual navega para `/projects`, não para o Kanban/último quadro definido na D52.
13. Há testes de filtros globais e proteção de escrita, mas faltam cobertura transversal de busca, relatórios, anexos, SignalR, caches e processamento assíncrono entre tenants.
14. Não há testes de associação única, ocultação do seletor comum, autorização do ciclo de vida por Administrador da plataforma, arquivamento read-only, restauração e concorrência.

## Test Gate Mapping

- Domínio: transições ativa/arquivada, preservação de dados e invariantes administrativas.
- .NET integração: platform admin versus perfis do tenant, isolamento de leitura/escrita, header obrigatório, arquivamento transacional e restauração.
- Persistência SQL Server: filtros, índices, ausência de vínculos cruzados e concorrência durante arquivamento.
- React/Vitest: seletor ausente para usuário comum, seletor do Platform Admin, descarte de estado anterior, ausência do onboarding de criação comum e superfícies read-only do control plane.
- Playwright: usuário comum preso ao único tenant; alternância administrativa sem reload; tentativa negada por admin do tenant e ciclo de arquivar/consultar/restaurar em ambiente E2E dedicado.

## Traceability

D17 + D55 + D58 → `SPEC-ORGANIZATIONS` → `TASK-034` → domínio/API de control plane + middleware/persistência multi-tenant + seletor superior → testes.
