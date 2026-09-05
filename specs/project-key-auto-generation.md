# SPEC-PROJECT-KEY-AUTO-GENERATION: Chave técnica interna do projeto

**Status:** approved
**Baseline:** target — decisão funcional aprovada em 2026-08-24; o código atual ainda possui divergências descritas nesta spec.

## Objective

Manter uma chave estável e única para cada projeto somente quando ela for tecnicamente necessária, sem expor sua criação, edição ou exibição nas telas normais do produto.

## Context

`Project.Key` já participa de unicidade no SQL Server, busca técnica, referências compostas de tarefas, importação e integrações. Esse uso interno pode ser preservado para compatibilidade, mas a chave não possui valor funcional para o usuário na experiência cotidiana.

Hoje o produto ainda apresenta `Chave (opcional — gerada do nome se vazia)`, aceita entrada manual e exibe o valor em cartões, cabeçalhos, seletores, relatórios e outras superfícies. Esse comportamento não representa mais a experiência aprovada.

## Scope

- Gerar a chave automaticamente no backend durante a criação do projeto.
- Não exibir campo de chave no formulário de criação.
- Não permitir que usuários informem ou editem a chave.
- Não exibir a chave em cartões, cabeçalhos, breadcrumbs, seletores, filtros, relatórios ou telas operacionais normais.
- Preservar `Project.Key` como identificador técnico interno enquanto for necessário para banco, unicidade, importação, busca técnica, compatibilidade ou integrações.
- Preservar as chaves dos projetos existentes.

## Out of Scope

- Remover imediatamente `Project.Key` do domínio, banco, índices, DTOs internos ou integrações.
- Renumerar projetos ou alterar chaves já persistidas.
- Usar a chave como nome, rótulo ou informação principal do projeto.
- Expor configuração administrativa para editar a chave.
- Criar migration sem auditoria técnica que demonstre uma alteração real de schema.

## Functional Requirements

1. O diálogo `Novo projeto` não apresenta campo de chave.
2. A API não depende de valor de chave fornecido pelo cliente para criar um projeto.
3. O backend gera uma chave técnica não vazia, normalizada e única no tenant.
4. Colisões são resolvidas automaticamente sem interação do usuário e sem criação parcial.
5. Usuários não podem editar a chave após a criação.
6. Telas normais identificam projetos pelo nome; quando houver ambiguidade, devem usar contexto funcional adicional que não obrigue o usuário a conhecer a chave técnica.
7. Contratos técnicos podem continuar transportando a chave somente quando um consumidor interno, importador ou integração realmente depender dela.
8. Uma futura remoção da chave exige auditoria de todos os consumidores, decisão própria e, se houver schema envolvido, `G-MIGRATION`.

## Invariants

- Enquanto persistida, a chave é obrigatória e única por organização.
- A geração acontece no servidor; o cliente não é autoridade sobre o identificador.
- Chaves existentes permanecem estáveis para não quebrar referências, importações ou integrações.
- Ocultar a chave da interface não altera a identidade primária do projeto nem substitui seu `Id`.
- Nenhuma falha de geração pode deixar projeto parcial persistido.

## Acceptance Criteria

- **Given** o diálogo `Novo projeto` **When** ele é aberto **Then** nenhum campo, ajuda ou opção de chave é exibido.
- **Given** um nome válido **When** o projeto é criado **Then** o backend gera e persiste automaticamente uma chave técnica única sem participação do usuário.
- **Given** colisão entre chaves derivadas **When** um novo projeto é criado **Then** o backend resolve a colisão automaticamente e conclui a criação uma única vez.
- **Given** listagens, cartões, cabeçalhos, breadcrumbs, seletores, filtros e relatórios **When** um projeto é apresentado **Then** seu nome é exibido sem a chave técnica.
- **Given** um projeto existente **When** a mudança de interface é aplicada **Then** sua chave persistida permanece inalterada.
- **Given** uma requisição de criação com chave enviada por cliente legado **When** ela é processada **Then** o valor do cliente não concede controle funcional sobre a chave; a política de compatibilidade deve preservar geração e unicidade sob autoridade do servidor.

## Data, Authorization and Contracts

- A decisão atual não exige migration: a coluna e o índice único continuam úteis e devem ser preservados.
- `Project.Id` continua sendo a identidade primária; `Project.Key` é um identificador técnico secundário.
- O contrato público de criação deve deixar de oferecer a chave como dado editável. Compatibilidade temporária com clientes legados pode aceitar e ignorar o campo, desde que o servidor continue sendo a única autoridade.
- DTOs de leitura devem omitir a chave das superfícies comuns; DTOs internos e integrações podem mantê-la quando houver dependência comprovada.
- As permissões de criação e administração de projeto permanecem as definidas na spec de acesso.

## Current Gaps

1. `Projects.tsx` exibe o campo opcional de chave e envia o valor digitado.
2. `Projects.tsx` mostra a chave no cartão do projeto.
3. `ProjectWorkspace.tsx` mostra a chave no cabeçalho.
4. Seletores e telas como relatórios, equipes, solicitações e dashboards exibem `project.key` junto ao nome.
5. O contrato `POST /api/projects` ainda aceita `Key` fornecida pelo cliente, e o backend a normaliza/persiste quando informada.
6. Testes atuais ainda usam e esperam a exposição da chave; faltam testes de geração exclusivamente server-side e ausência visual transversal.
7. Consumidores técnicos legítimos existem hoje em busca global, dependências, portal externo, relatórios e importação; eles precisam ser classificados antes de qualquer tentativa futura de remover a coluna.

## Test Gate Mapping

- React/Vitest: campo ausente na criação e chave ausente em todas as superfícies normais de projeto.
- .NET/xUnit: geração server-side, normalização, unicidade por organização, colisão automática e preservação de chaves existentes.
- API/integration: criação sem chave fornecida pelo cliente e compatibilidade segura com payload legado.
- Playwright: criar projeto informando somente dados funcionais e confirmar que nenhuma chave aparece na jornada normal.

## Traceability

Decisão humana de 2026-08-24 → `SPEC-PROJECT-KEY-AUTO-GENERATION` → `TASK-024` → geração técnica no backend + remoção da entrada/edição/exibição no frontend → testes.
